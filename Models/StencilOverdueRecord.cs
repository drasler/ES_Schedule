using System;

namespace ES_Schedule.Models
{
    /// <summary>
    /// 逾期鋼板資料模型
    /// </summary>
    public class StencilOverdueRecord
    {
        /// <summary>
        /// 鋼板ID
        /// </summary>
        public int SteelPlateId { get; set; }

        /// <summary>
        /// 鋼板編號
        /// </summary>
        public string SteelPlateNo { get; set; }

        /// <summary>
        /// 工程編號
        /// </summary>
        public string EngNo { get; set; }

        /// <summary>
        /// 儲存位置
        /// </summary>
        public string StorageLocation { get; set; }

        /// <summary>
        /// 可使用次數
        /// </summary>
        public int MaxUseTimes { get; set; }

        /// <summary>
        /// 已使用次數
        /// </summary>
        public int UsedTimes { get; set; }

        /// <summary>
        /// 使用率 (%)
        /// </summary>
        public decimal UsageRate { get; set; }

        /// <summary>
        /// 是否已發送告警 (Y/N)
        /// </summary>
        public string IsAlerted { get; set; }

        /// <summary>
        /// 目前工單號碼
        /// </summary>
        public string CurrentWipNo { get; set; }

        /// <summary>
        /// 上線日期 (格式化)
        /// </summary>
        public string OnlineDate { get; set; }

        /// <summary>
        /// 上線日期 (原始)
        /// </summary>
        public DateTime? OnlineDateRaw { get; set; }

        /// <summary>
        /// 在線天數
        /// </summary>
        public int? DaysOnline { get; set; }

        /// <summary>
        /// 建立者
        /// </summary>
        public string CreateBy { get; set; }

        /// <summary>
        /// 告警層級 (警告/嚴重/緊急)
        /// </summary>
        public string AlertLevel { get; set; }

        /// <summary>
        /// 逾期原因
        /// </summary>
        public string OverdueReason { get; set; }
    }
}
