﻿using System;
using System.Collections.Generic;
using TatehamaInterlockingConsole.Factories;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.Services;

namespace TatehamaInterlockingConsole.Manager
{
    /// <summary>
    /// GlobalData管理クラス
    /// </summary>
    public class DataManager
    {
        private static readonly DataManager _instance = new();
        private TimeService _timeService;
        public event Action<DateTime> TimeUpdated;

        public static DataManager Instance => _instance;

        /// <summary>
        /// 現在時刻
        /// </summary>
        public DateTime CurrentTime => _timeService?.CurrentTime ?? DateTime.MinValue;

        /// <summary>
        /// 音量
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// 全コントロール設定データ
        /// </summary>
        public List<UIControlSetting> AllControlSettingList { get; set; }

        /// <summary>
        /// 列番表示画像Pathを格納した辞書データ
        /// </summary>
        public Dictionary<string, string> RetsubanImagePathDictionary { get; set; }

        /// <summary>
        /// 駅設定リストデータ
        /// </summary>
        public List<StationSetting> StationSettingList { get; set; }

        /// <summary>
        /// 接近警報鳴動条件リストデータ
        /// </summary>
        public List<ApproachingAlarmSetting> ApproachingAlarmConditionList { get; set; }

        /// <summary>
        /// サーバー接続状態
        /// </summary>
        public bool ServerConnected { get; set; }

        private DatabaseOperational.DataFromServer _dataFromServer;
        /// <summary>
        /// サーバー受信データ
        /// </summary>
        public DatabaseOperational.DataFromServer DataFromServer
        {
            get => _dataFromServer;
            set
            {
                _dataFromServer = value;
            }
        }

        private DatabaseOperational.TraincrewRole _authentication;
        /// <summary>
        /// サーバー受信データ(認証情報)
        /// </summary>
        public DatabaseOperational.TraincrewRole Authentication
        {
            get => _authentication;
            set
            {
                _authentication = value;
            }
        }

        private List<string> _activeStationsList;
        /// <summary>
        /// 起動しているウィンドウの駅名を保持するリスト
        /// </summary>
        public List<string> ActiveStationsList
        {
            get => _activeStationsList;
            set
            {
                if (_activeStationsList != value)
                {
                    _activeStationsList = value;
                }
            }
        }

        private List<ActiveAlarmList> _activeAlarmsList;
        /// <summary>
        /// 鳴動している接近警報名を保持するリスト
        /// </summary>
        public List<ActiveAlarmList> ActiveAlarmsList
        {
            get => _activeAlarmsList;
            set
            {
                if (_activeAlarmsList != value)
                {
                    _activeAlarmsList = value;
                }
            }
        }

        private bool _flagValue;
        /// <summary>
        /// 一定周期でON・OFFを切り替えるフラグ
        /// </summary>
        public bool FlagValue
        {
            get => _flagValue;
            set
            {
                if (_flagValue != value)
                {
                    _flagValue = value;
                }
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        private DataManager()
        {
            AllControlSettingList = new();
            RetsubanImagePathDictionary = new();
            StationSettingList = new();
            DataFromServer = new();
            ActiveStationsList = new();
            ActiveAlarmsList = new();
            ApproachingAlarmConditionList = new();
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        /// <param name="timeService"></param>
        public void Initialize(TimeService timeService)
        {
            _timeService = timeService;
            _timeService.TimeUpdated += (currentTime) => OnTimeUpdated();
            TimeUpdated += (currentTime) => ClockImageFactory.CurrentTime = currentTime;
        }

        /// <summary>
        /// 駅名を追加するメソッド
        /// </summary>
        /// <param name="stationName"></param>
        public void AddActiveStation(string stationName)
        {
            if (!_activeStationsList.Contains(stationName))
            {
                _activeStationsList.Add(stationName);
            }
        }

        /// <summary>
        /// 駅名を削除するメソッド
        /// </summary>
        /// <param name="stationName"></param>
        public void RemoveActiveStation(string stationName)
        {
            if (_activeStationsList.Contains(stationName))
            {
                _activeStationsList.Remove(stationName);
            }
        }

        /// <summary>
        /// 時刻データ変更通知イベント
        /// </summary>
        private void OnTimeUpdated()
        {
            TimeUpdated?.Invoke(CurrentTime);
        }
    }

    /// <summary>
    /// 鳴動処理用接近警報データクラス
    /// </summary>
    public class ActiveAlarmList
    {
        /// <summary>
        /// 自駅名
        /// </summary>
        public string StationName { get; set; }
        /// <summary>
        /// 方向
        /// </summary>
        public bool IsUpSide { get; set; }
    }
}
