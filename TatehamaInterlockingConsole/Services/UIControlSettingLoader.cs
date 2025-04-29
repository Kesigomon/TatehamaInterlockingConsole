using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TatehamaInterlockingConsole.Helpers;
using TatehamaInterlockingConsole.Models;

namespace TatehamaInterlockingConsole.Services
{
    /// <summary>
    /// UIControlSettingList読込クラス
    /// </summary>
    public static class UIControlSettingLoader
    {
        /// <summary>
        /// TSVファイルを読み込みUIControlSettingListを作成する
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<UIControlSetting> LoadSettings(string filePath)
        {
            try
            {
                var settings = new List<UIControlSetting>();
                bool header = false;

                // ファイルのエンコーディングを判別
                Encoding fileEncoding = DataHelper.ReadFileWithEncodingDetection(filePath);
                string[] lines = File.ReadAllLines(filePath, fileEncoding);

                foreach (var line in lines)
                {
                    try
                    {
                        // ヘッダー行はスキップ
                        if (!header)
                        {
                            header = true;
                            continue;
                        }

                        // 行に何も無ければスキップ
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var columns = line.Split('\t');

                        // ControlTypeが未定義ならスキップ
                        if (string.IsNullOrWhiteSpace(columns[(int)EnumData.ColumnIndex.ControlType]))
                        {
                            continue;
                        }

                        // 駅名称抽出
                        var stationName = DataHelper.ExtractStationNameFromFilePath(filePath);
                        var stationNumber = DataHelper.GetStationNumberFromStationName(stationName);
                        // ImagePattern生成
                        var imagePattern = columns[(int)EnumData.ColumnIndex.ImagePattern].Split(',').Select(pattern => pattern.Trim('"').Trim()).ToList();

                        settings.Add(new UIControlSetting
                        {
                            StationName = stationName,
                            StationNumber = stationNumber,
                            ControlType = columns[(int)EnumData.ColumnIndex.ControlType] != string.Empty ? columns[(int)EnumData.ColumnIndex.ControlType] : string.Empty,
                            UniqueName = columns[(int)EnumData.ColumnIndex.UniqueName],
                            ParentName = columns[(int)EnumData.ColumnIndex.ParentName],
                            ServerType = columns[(int)EnumData.ColumnIndex.ServerType],
                            ServerName = columns[(int)EnumData.ColumnIndex.ServerName],
                            PointNameA = columns[(int)EnumData.ColumnIndex.PointNameA],
                            PointValueA = columns[(int)EnumData.ColumnIndex.PointValueA] == "N" ? EnumData.NRC.Normal : columns[(int)EnumData.ColumnIndex.PointValueA] == "R" ? EnumData.NRC.Reversed : EnumData.NRC.Center,
                            PointNameB = columns[(int)EnumData.ColumnIndex.PointNameB],
                            PointValueB = columns[(int)EnumData.ColumnIndex.PointValueB] == "N" ? EnumData.NRC.Normal : columns[(int)EnumData.ColumnIndex.PointValueB] == "R" ? EnumData.NRC.Reversed : EnumData.NRC.Center,
                            DirectionName = columns[(int)EnumData.ColumnIndex.DirectionName],
                            DirectionValue = columns[(int)EnumData.ColumnIndex.DirectionValue] == "L" ? EnumData.LNR.Left : columns[(int)EnumData.ColumnIndex.DirectionValue] == "N" ? EnumData.LNR.Normal : EnumData.LNR.Right,
                            X = double.TryParse(columns[(int)EnumData.ColumnIndex.X], out var x) ? x : 0,
                            Y = double.TryParse(columns[(int)EnumData.ColumnIndex.Y], out var y) ? y : 0,
                            RelativeX = double.TryParse(columns[(int)EnumData.ColumnIndex.X], out var relativeX) ? relativeX : 0,
                            RelativeY = double.TryParse(columns[(int)EnumData.ColumnIndex.Y], out var relativeY) ? relativeY : 0,
                            Width = double.TryParse(columns[(int)EnumData.ColumnIndex.Width], out var width) ? width : 0,
                            Height = double.TryParse(columns[(int)EnumData.ColumnIndex.Height], out var height) ? height : 0,
                            Angle = double.TryParse(columns[(int)EnumData.ColumnIndex.Angle], out var angle) ? angle : 0,
                            AngleOriginX = double.TryParse(columns[(int)EnumData.ColumnIndex.AngleOriginX], out var angleX) ? angleX : 0.5,
                            AngleOriginY = double.TryParse(columns[(int)EnumData.ColumnIndex.AngleOriginY], out var angleY) ? angleY : 0.5,
                            Text = columns[(int)EnumData.ColumnIndex.Text],
                            FontSize = double.TryParse(columns[(int)EnumData.ColumnIndex.FontSize], out var fontSize) ? fontSize : 10,
                            BackgroundColor = columns[(int)EnumData.ColumnIndex.BackgroundColor],
                            TextColor = columns[(int)EnumData.ColumnIndex.TextColor],
                            ClickEventName = columns[(int)EnumData.ColumnIndex.ClickEventName],
                            ImagePattern = imagePattern,
                            ImagePatternSymbol = MapImagePatternsToSymbols(imagePattern),
                            ImageIndex = int.TryParse(columns[(int)EnumData.ColumnIndex.ImageIndex], out var defaultImage) ? defaultImage : 0,
                            BaseImagePath = AppDomain.CurrentDomain.BaseDirectory + columns[(int)EnumData.ColumnIndex.BaseImagePath].Trim('"').Trim(),
                            ImagePaths = CreateImagePaths(columns[(int)EnumData.ColumnIndex.ImagePattern], columns[(int)EnumData.ColumnIndex.ImagePath]),
                            KeyInserted = false,
                            Retsuban = string.Empty,
                            IsHandling = false,
                            IsButtionRaised = false,
                            IsButtionDroped = false,
                            Remark = columns[(int)EnumData.ColumnIndex.Remark],
                        });
                    }
                    catch (DecoderFallbackException)
                    {
                        // デコードエラー時の処理
                        continue;
                    }
                }
                return settings;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                return null;
            }
        }

        /// <summary>
        /// ImagePaths作成処理
        /// </summary>
        /// <param name="imagePatternColumn"></param>
        /// <param name="imagePathColumn"></param>
        /// <returns></returns>
        private static Dictionary<int, string> CreateImagePaths(string imagePatternColumn, string imagePathColumn)
        {
            var imagePaths = new Dictionary<int, string>();

            // ImagePatternとImagePathをそれぞれ分割して処理
            var patterns = imagePatternColumn
                .Split(',')
                .Select(p => p.Trim('"').Trim())
                .Where(p => int.TryParse(p, out _))
                .Select(int.Parse)
                .ToList();

            var paths = imagePathColumn
                .Split(',')
                .Select(p => p.Trim('"').Trim())
                .ToList();

            // パターンとパスの個数が多い方を取得
            var maxCount = Math.Max(patterns.Count, paths.Count);

            for (int i = 0; i < maxCount; i++)
            {
                // patternsとpathsを対応付け、インデックス範囲外の場合はデフォルト値を使用
                var pattern = i < patterns.Count ? patterns[i] : i;
                var path = i < paths.Count ? paths[i] : string.Empty;
                imagePaths[pattern] = AppDomain.CurrentDomain.BaseDirectory + path;
            }
            return imagePaths;
        }

        private static readonly Dictionary<string, string> PatternToSymbolMap = new Dictionary<string, string>()
        {
            { "0,1", "NR" },
            { "-1,0", "LN" },
            { "-1,1", "LR" },
            { "-1,0,1", "LNR" },
            { "0,1,10,11", "KeyNR" },
            { "-1,0,-11,10", "KeyLN" },
            { "-1,1,-11,11", "KeyLR" },
            { "-1,0,1,-11,10,11", "KeyLNR" }
        };

        /// <summary>
        /// ImagePatternをImagePatternSymbolに変換するメソッド
        /// </summary>
        /// <param name="settings">UIControlSettingのリスト</param>
        public static string MapImagePatternsToSymbols(List<string> imagePattern)
        {
            string imagePatternSymbol;

            if (imagePattern == null || !imagePattern.Any())
            {
                return string.Empty;
            }

            // ImagePatternをカンマ区切りの文字列に変換
            var patternKey = string.Join(",", imagePattern);

            // 対照表を使用して変換
            if (PatternToSymbolMap.TryGetValue(patternKey, out var symbol))
            {
                imagePatternSymbol = symbol;
            }
            else
            {
                // 対応するシンボルがない場合は未設定
                imagePatternSymbol = string.Empty;
            }
            return imagePatternSymbol;
        }
    }
}