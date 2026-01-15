using System;

namespace ES_Schedule.Models
{
    /// <summary>
    /// 報工明細模型 - 對應 JHAMES.ACTUAL_TIME_DETAIL
    /// </summary>
    public class ActualTimeDetail
    {
        /// <summary>
        /// 報工ID (關聯主表)
        /// </summary>
        public int ACTUAL_ID { get; set; }

        /// <summary>
        /// 明細ID
        /// </summary>
        public int ACTUAL_DETAIL_ID { get; set; }

        /// <summary>
        /// 工單號碼
        /// </summary>
        public string WIP_NO { get; set; }

        /// <summary>
        /// 時間表ID
        /// </summary>
        public int BARCODE_ID { get; set; }

        /// <summary>
        /// 使用者ID
        /// </summary>
        public int USER_ID { get; set; }

        /// <summary>
        /// 站別ID
        /// </summary>
        public int STATION_ID { get; set; }

        /// <summary>
        /// 製程代碼
        /// </summary>
        public string UNIT_NO { get; set; }

        /// <summary>
        /// 結束時間
        /// </summary>
        public DateTime PASS_DATETIME { get; set; }

        /// <summary>
        /// 開始時間
        /// </summary>
        public DateTime PASS_DATETIME_S { get; set; }

        /// <summary>
        /// 人數
        /// </summary>
        public int A_CNT { get; set; }

        /// <summary>
        /// 實際工時
        /// </summary>
        public string A_CT { get; set; }

        /// <summary>
        /// 數量
        /// </summary>
        public string S_CT { get; set; }

        /// <summary>
        /// 最後站別ID
        /// </summary>
        public int LAST_STATION_ID { get; set; }

        /// <summary>
        /// 建立時間
        /// </summary>
        public DateTime CREATE_DATETIME { get; set; }

        /// <summary>
        /// 休息時間
        /// </summary>
        public string REST_CT { get; set; }

        /// <summary>
        /// 製程路線
        /// </summary>
        public string ROUTE { get; set; }
    }
}
