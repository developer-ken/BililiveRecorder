﻿using BililiveRecorder.FlvProcessor;
using Newtonsoft.Json;
using NLog;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BililiveRecorder.Core.Config
{
    [JsonObject(memberSerialization: MemberSerialization.OptIn)]
    public class ConfigV1 : INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 当前工作目录
        /// </summary>
        [JsonIgnore]
        [Utils.DoNotCopyProperty]
        public string WorkDirectory { get => _workDirectory; set => SetField(ref _workDirectory, value); }


        /// <summary>
        /// 房间号列表
        /// </summary>
        [JsonProperty("roomlist")]
        public List<RoomV1> RoomList { get; set; } = new List<RoomV1>();

        /// <summary>
        /// 启用的功能
        /// </summary>
        [JsonProperty("feature")]
        public EnabledFeature EnabledFeature { get => _enabledFeature; set => SetField(ref _enabledFeature, value); }

        /// <summary>
        /// 剪辑-过去的时长(秒)
        /// </summary>
        [JsonProperty("clip_length_future")]
        public uint ClipLengthFuture { get => _clipLengthFuture; set => SetField(ref _clipLengthFuture, value); }

        /// <summary>
        /// 剪辑-将来的时长(秒)
        /// </summary>
        [JsonProperty("clip_length_past")]
        public uint ClipLengthPast { get => _clipLengthPast; set => SetField(ref _clipLengthPast, value); }

        /// <summary>
        /// 自动切割模式
        /// </summary>
        [JsonProperty("cutting_mode")]
        public AutoCuttingMode CuttingMode { get => _cuttingMode; set => SetField(ref _cuttingMode, value); }

        /// <summary>
        /// 自动切割数值（分钟/MiB）
        /// </summary>
        [JsonProperty("cutting_number")]
        public uint CuttingNumber { get => _cuttingNumber; set => SetField(ref _cuttingNumber, value); }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) { return false; }
            logger.Debug("设置 [{0}] 的值已从 [{1}] 修改到 [{2}]", propertyName, field, value);
            field = value; OnPropertyChanged(propertyName); return true;
        }
        #endregion

        private uint _clipLengthPast = 20;
        private uint _clipLengthFuture = 10;
        private uint _cuttingNumber = 10;
        private EnabledFeature _enabledFeature = EnabledFeature.Both;
        private AutoCuttingMode _cuttingMode = AutoCuttingMode.Disabled;
        private string _workDirectory;
    }
}
