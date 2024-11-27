﻿using System;
using System.Collections.Generic;
using TatehamaInterlockinglConsole.Factories;
using TatehamaInterlockinglConsole.Models;
using TatehamaInterlockinglConsole.Helpers;

namespace TatehamaInterlockinglConsole.Manager
{
    /// <summary>
    /// GlobalData管理クラス
    /// </summary>
    public class DataManager
    {
        private static DataManager _instance = new DataManager();
        private TimeService _timeService;
        public event Action<DateTime> TimeUpdated;

        public static DataManager Instance => _instance;
        public DateTime CurrentTime => _timeService?.CurrentTime ?? DateTime.MinValue;

        /// <summary>
        /// 全コントロール設定データ
        /// </summary>
        public List<UIControlSetting> AllControlSettingList { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        private DataManager()
        {
            AllControlSettingList = new List<UIControlSetting>();
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
        /// 時刻データ変更通知イベント
        /// </summary>
        private void OnTimeUpdated()
        {
            TimeUpdated?.Invoke(CurrentTime);
        }
    }
}
