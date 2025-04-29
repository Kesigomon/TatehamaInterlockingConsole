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
    /// ApproachingAlarmConditionList読込クラス
    /// </summary>
    public static class ApproachingAlarmSettingLoader
    {
        /// <summary>
        /// TSVファイルを読み込みApproachingAlarmSettingListを作成する
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static List<ApproachingAlarmSetting> LoadSettings(string folderPath, string fileName)
        {
            try
            {
                // ファイルパスを組み立てる
                string filePath = Path.Combine(folderPath, fileName);

                var Settings = new List<ApproachingAlarmSetting>();
                bool header = false;

                // ファイルのエンコーディングを判別
                Encoding fileEncoding = DataHelper.ReadFileWithEncodingDetection(filePath);
                string[] lines = File.ReadAllLines(filePath, fileEncoding);

                foreach (var line in lines)
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

                    Settings.Add(
                        new()
                        {
                            IsAlarmConditionMet = false,
                            IsAlarmPlayed = false,
                            StationName = columns[0],
                            OtherStationNameA = columns[1],
                            OtherStationNameB = columns[2],
                            IsUpSide = (columns[3] == "UP"),
                            TrackName = new()
                            {
                                Name = FormatName(columns[4]),
                                Station = GetStation(columns[4], columns),
                                IsReversePosition = IsReversePosition(columns[4]),
                                Type = GetType(columns[4])
                            },
                            ConditionsList = columns[5].Split(':').Select(condition =>
                            {
                                var formattedName = FormatName(condition);
                                var type = GetType(condition);
                                var station = GetStation(condition, columns);
                                var isReversePosition = IsReversePosition(condition);

                                return new ApproachingAlarmType
                                {
                                    Name = FormatToServerName(formattedName, type, station),
                                    Station = station,
                                    IsReversePosition = isReversePosition,
                                    Type = type
                                };
                            }).ToList()
                        });
                }
                return Settings;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                return null;
            }
        }

        /// <summary>
        /// 名称をサーバー名に整形
        /// </summary>
        /// <param name="formattedName"></param>
        /// <param name="type"></param>
        /// <param name="station"></param>
        /// <returns></returns>
        private static string FormatToServerName(string formattedName, string type, string station)
        {
            // 転てつ器なら"[所属駅名]_W[転てつ器番号]"に整形
            if (type == "Point")
            {
                return station + "_W" + formattedName;
            }
            // 方向てこなら"[所属駅名]_[てこ番号]F"に整形
            else if (type == "Direction")
            {
                return station + "_" + formattedName.Replace("L", "").Replace("R", "") + "F";
            }
            // 信号機・軌道回路・その他種別ならそのまま
            else
            {
                return formattedName;
            }
        }

        /// <summary>
        /// 所属駅名判定
        /// </summary>
        /// <param name="input"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        private static string GetStation(string input, string[] columns)
        {
            if (input.StartsWith("[[") && input.EndsWith("]]"))
                return columns[2];
            else if (input.StartsWith("[") && input.EndsWith("]"))
                return columns[1];
            else
                return columns[0];
        }

        /// <summary>
        /// 種別判定
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string GetType(string input)
        {
            var strName = FormatName(input);

            if (strName.EndsWith("T"))
                return "Track";
            else if (strName.Contains("出発") || strName.Contains("場内") || strName.Contains("入換"))
                return "Signal";
            else if (strName.Contains("L") || strName.Contains("R"))
                return "Direction";
            else if (int.TryParse(strName, out _))
                return "Point";
            else if (strName.Contains("列番"))
                return "Retsuban";
            else
                return "None";
        }

        /// <summary>
        /// 反位判定
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static bool IsReversePosition(string input)
        {
            string strName = FormatName(input, true);
            return strName.StartsWith("(") && strName.EndsWith(")");
        }

        /// <summary>
        /// 名称整形
        /// </summary>
        /// <param name="input"></param>
        /// <param name="isReverseCheck"></param>
        /// <returns></returns>
        private static string FormatName(string input, bool isReverseCheck = false)
        {
            if (isReverseCheck)
                return input.Replace("[[", "").Replace("]]", "").Replace("[", "").Replace("]", "");
            else
                return input.Replace("[[", "").Replace("]]", "").Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "");
        }
    }
}
