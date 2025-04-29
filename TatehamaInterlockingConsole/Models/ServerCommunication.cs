using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Services;
using TatehamaInterlockingConsole.ViewModels;

namespace TatehamaInterlockingConsole.Models
{
    /// <summary>
    /// サーバー通信クラス
    /// </summary>
    public class ServerCommunication
    {
        private string _token;
        private readonly OpenIddictClientService _openIddictClientService;
        private readonly DataManager _dataManager;
        private readonly DataUpdateViewModel _dataUpdateViewModel;
        private static HubConnection _connection;
        private static bool _isUpdateLoopRunning = false;
        private const string HubConnectionName = "interlocking";

        /// <summary>
        /// サーバー接続状態変更イベント
        /// </summary>
        public event Action<bool> ConnectionStatusChanged;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ServerCommunication(OpenIddictClientService openIddictClientService)
        {
            _openIddictClientService = openIddictClientService;
            _dataManager = DataManager.Instance;
            _dataUpdateViewModel = DataUpdateViewModel.Instance;

            if (!_isUpdateLoopRunning)
            {
                _isUpdateLoopRunning = true;

                // ループ処理開始
                Task.Run(() => UpdateLoop());
            }
        }

        /// <summary>
        /// ループ処理
        /// </summary>
        /// <returns></returns>
        private async Task UpdateLoop()
        {
            while (true)
            {
                var timer = Task.Delay(50);

                // サーバー接続状態変更イベント発火
                ConnectionStatusChanged?.Invoke(_dataManager.ServerConnected);

                // サーバー接続中ならデータ送信
                if (_dataManager.ServerConnected)
                {
                    await SendConstantDataRequestToServerAsync(_dataManager.ActiveStationsList);
                }
                await timer;
            }
        }

        /// <summary>
        /// ユーザー認証
        /// </summary>
        /// <returns></returns>
        public async Task AuthenticateAsync()
        {
            if (ServerAddress.IsDebug)
            {
                await InitializeConnection();
                return;
            }
            try
            {
                using var source = new CancellationTokenSource(TimeSpan.FromSeconds(90));

                // ブラウザで認証要求
                var result = await _openIddictClientService.ChallengeInteractivelyAsync(new()
                {
                    CancellationToken = source.Token
                });

                // 認証完了まで待機
                var resultAuth = await _openIddictClientService.AuthenticateInteractivelyAsync(new()
                {
                    CancellationToken = source.Token,
                    Nonce = result.Nonce
                });

                // 認証成功(トークン取得)
                _token = resultAuth.BackchannelAccessToken;

                // サーバー接続初期化
                await InitializeConnection();
            }
            catch (OpenIddictExceptions.ProtocolException exception) when (exception.Error == OpenIddictConstants.Errors.AccessDenied)
            {
                // ログインしたユーザーがサーバーにいないか、入鋏ロールがついてない
                CustomMessage.Show("認証が拒否されました。\n司令主任に連絡してください。", "認証拒否", exception, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (OpenIddictExceptions.ProtocolException exception) when (exception.Error == OpenIddictConstants.Errors.ServerError)
            {
                // サーバーでトラブル発生
                var result = CustomMessage.Show("認証時にサーバーでエラーが発生しました。\\n再認証しますか？", "サーバーエラー", exception, MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (result == MessageBoxResult.Yes)
                {
                    _ = AuthenticateAsync();
                }
            }
            catch (Exception exception)
            {
                // その他別な理由で認証失敗
                var result = CustomMessage.Show("認証に失敗しました。\n再認証しますか？", "認証失敗", exception, MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (result == MessageBoxResult.Yes)
                {
                    _ = AuthenticateAsync();
                }
            }
        }

        /// <summary>
        /// サーバー接続初期化
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task InitializeConnection()
        {
            // HubConnectionの作成
            _connection = new HubConnectionBuilder()
                .WithUrl($"{ServerAddress.SignalAddress}/hub/{HubConnectionName}?access_token={_token}")
                .WithAutomaticReconnect() // 自動再接続
                .Build();

            // サーバー接続
            while (!_dataManager.ServerConnected)
            {
                try
                {
                    await _connection.StartAsync();
                    Debug.WriteLine("Connected");
                    _dataManager.ServerConnected = true;
                }
                catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
                {
                    // 該当Hubにアクセスするためのロールが無い
                    CustomMessage.Show("接続が拒否されました。\n付与されたロールを確認の上、司令主任に連絡してください。", "ロール不一致", exception, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Connection Error!! {exception.Message}");
                    _dataManager.ServerConnected = false;

                    var result = CustomMessage.Show("接続に失敗しました。\n再接続しますか？", "接続失敗", exception, MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // 再接続イベントのハンドリング
            _connection.Reconnecting += exception =>
            {
                _dataManager.ServerConnected = false;
                Debug.WriteLine("Reconnecting");
                return Task.CompletedTask;
            };

            _connection.Reconnected += exeption =>
            {
                _dataManager.ServerConnected = true;
                Debug.WriteLine("Connected");
                return Task.CompletedTask;
            };
            await Task.Delay(Timeout.Infinite);
        }

        /// <summary>
        /// サーバーへ常時送信用データをリクエスト
        /// </summary>
        /// <param name="activeStationsList"></param>
        /// <returns></returns>
        public async Task SendConstantDataRequestToServerAsync(List<string> activeStationsList)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.DataFromServer>("SendData_Interlocking", activeStationsList);
                try
                {
                    if (data != null)
                    {
                        // 運用クラスに代入
                        if (_dataManager.DataFromServer == null)
                        {
                            _dataManager.DataFromServer = data;
                        }
                        else
                        {
                            // 変更があれば更新
                            foreach (var property in data.GetType().GetProperties())
                            {
                                var newValue = property.GetValue(data);
                                var oldValue = property.GetValue(_dataManager.DataFromServer);
                                if (newValue != null && !newValue.Equals(oldValue))
                                {
                                    property.SetValue(_dataManager.DataFromServer, newValue);
                                }
                            }
                        }

                        // 方向てこ情報を保存
                        if (data.Directions != null)
                        {
                            _dataManager.DirectionStateList = data.Directions.Select(d =>
                            {
                                var existingDirection = _dataManager.DirectionStateList?.FirstOrDefault(ds => ds.Name == d.Name);
                                if (existingDirection != null)
                                {
                                    // 値が変更されている場合のみ更新
                                    if (existingDirection.State != d.State)
                                    {
                                        existingDirection.State = d.State;
                                        existingDirection.UpdateTime = DateTime.Now;
                                        existingDirection.IsAlarmPlayed = false;
                                    }
                                    return existingDirection;
                                }
                                else
                                {
                                    // 新しいデータの場合は追加
                                    return new DirectionStateList
                                    {
                                        Name = d.Name,
                                        State = d.State,
                                        UpdateTime = DateTime.Now,
                                        IsAlarmPlayed = false
                                    };
                                }
                            }).ToList();
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);

                        // 物理ボタン情報を前回の値として保存
                        if (data.PhysicalButtons != null)
                        {
                            _dataManager.PhysicalButtonOldList = data.PhysicalButtons.Select(d => new DatabaseOperational.DestinationButtonData
                            {
                                Name = d.Name,
                                IsRaised = d.IsRaised,
                                OperatedAt = d.OperatedAt
                            }).ToList();
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send constant data to server: {ex.Message}");
            }
        }

        /// <summary>
        /// サーバーへ物理てこイベント送信用データをリクエスト
        /// </summary>
        /// <param name="leverData"></param>
        /// <returns></returns>
        public async Task SendLeverEventDataRequestToServerAsync(DatabaseOperational.LeverData leverData)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.LeverData>("SetPhysicalLeverData", leverData);
                try
                {
                    if (data != null)
                    {
                        // 変更があれば更新
                        foreach (var property in data.GetType().GetProperties())
                        {
                            var newValue = property.GetValue(data);
                            var oldValue = property.GetValue(_dataManager.DataFromServer);
                            if (newValue != null && !newValue.Equals(oldValue))
                            {
                                property.SetValue(_dataManager.DataFromServer, newValue);
                            }
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
            }
        }

        /// <summary>
        /// サーバーへ物理鍵てこイベント送信用データをリクエスト
        /// </summary>
        /// <param name="keyLeverData"></param>
        /// <returns></returns>
        public async Task SendKeyLeverEventDataRequestToServerAsync(DatabaseOperational.KeyLeverData keyLeverData)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.KeyLeverData>("SetPhysicalKeyLeverData", keyLeverData);
                try
                {
                    if (data != null)
                    {
                        // 変更があれば更新
                        foreach (var property in data.GetType().GetProperties())
                        {
                            var newValue = property.GetValue(data);
                            var oldValue = property.GetValue(_dataManager.DataFromServer);
                            if (newValue != null && !newValue.Equals(oldValue))
                            {
                                property.SetValue(_dataManager.DataFromServer, newValue);
                            }
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
            }
        }

        /// <summary>
        /// サーバーへ着点ボタンイベント送信用データをリクエスト
        /// </summary>
        /// <param name="buttonData"></param>
        /// <returns></returns>
        public async Task SendButtonEventDataRequestToServerAsync(DatabaseOperational.DestinationButtonData buttonData)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.DestinationButtonData>("SetDestinationButtonState", buttonData);
                try
                {
                    if (data != null)
                    {
                        // 変更があれば更新
                        foreach (var property in data.GetType().GetProperties())
                        {
                            var newValue = property.GetValue(data);
                            var oldValue = property.GetValue(_dataManager.DataFromServer);
                            if (newValue != null && !newValue.Equals(oldValue))
                            {
                                property.SetValue(_dataManager.DataFromServer, newValue);
                            }
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);

                        // 物理ボタン情報を前回の値として保存
                        if (_dataManager.PhysicalButtonOldList != null)
                        {
                            var existingButton = _dataManager.PhysicalButtonOldList
                                .FirstOrDefault(b => b.Name == data.Name);

                            if (existingButton != null)
                            {
                                existingButton.IsRaised = data.IsRaised;
                                existingButton.OperatedAt = data.OperatedAt;
                            }
                            else
                            {
                                _dataManager.PhysicalButtonOldList.Add(new DatabaseOperational.DestinationButtonData
                                {
                                    Name = data.Name,
                                    IsRaised = data.IsRaised,
                                    OperatedAt = data.OperatedAt
                                });
                            }
                        }
                        else
                        {
                            _dataManager.PhysicalButtonOldList = new List<DatabaseOperational.DestinationButtonData>
                            {
                                new DatabaseOperational.DestinationButtonData
                                {
                                    Name = data.Name,
                                    IsRaised = data.IsRaised,
                                    OperatedAt = data.OperatedAt
                                }
                            };
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
            }
        }

        /// <summary>
        /// サーバー切断
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
        }
    }
}
