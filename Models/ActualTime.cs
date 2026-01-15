using System;

namespace ES_Schedule.Models
{
    /// <summary>
    /// 報工主表模型 - 對應 JHAMES.ACTUAL_TIME
    /// </summary>
    public class ActualTime
    {
        /// <summary>
        /// 報工ID (主鍵)
        /// </summary>
        public int ACTUAL_ID { get; set; }

        /// <summary>
        /// 報工日期
        /// </summary>
        public DateTime ACTUAL_DATE { get; set; }

        /// <summary>
        /// 工單號碼
        /// </summary>
        public string WIP_NO { get; set; }

        /// <summary>
        /// 製程代碼 (S/D/T/P/B)
        /// </summary>
        public string UNIT_NO { get; set; }

        /// <summary>
        /// 實際生產工時
        /// </summary>
        public string PRODUCTION_TIME { get; set; }

        /// <summary>
        /// 建立日期
        /// </summary>
        public DateTime? CREATE_DATE { get; set; }

        /// <summary>
        /// 生產數量
        /// </summary>
        public int? PRODUCTION_CNT { get; set; }

        /// <summary>
        /// 製程路線 (0010/0020/0030)
        /// </summary>
        public string ROUTE { get; set; }

        /// <summary>
        /// 資料來源
        /// </summary>
        public string SOURCE { get; set; }

        /// <summary>
        /// SAP 報工數量
        /// </summary>
        public int? PRODUCTION_CNT_SAP { get; set; }
    }
}
