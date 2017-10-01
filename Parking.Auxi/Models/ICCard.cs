using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    /// <summary>
    /// 用户卡号
    /// </summary>
    public class ICCard
    {
      
        public int ID { get; set; }
      
        /// <summary>
        /// 物理卡号
        /// </summary>
        public string PhysicCode { get; set; }
      
        /// <summary>
        /// 用户卡号,4位用户卡号
        /// </summary>
        public string UserCode { get; set; }       
        public EnmICCardStatus Status { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime LossDate { get; set; }
        public DateTime LogoutDate { get; set; }
        /// <summary>
        /// 顾客ID
        /// </summary>     
        public int CustID { get; set; }  
    }

    public enum EnmICCardType
    {
       
        Init = 0,  //初始       
        Temp,    //临时       
        Periodical,   //定期       
        FixedLocation,  //固定卡
        VIP,     //贵宾卡，不需要充值，直接放行
    }
    public enum EnmICCardStatus
    {
        Init = 0,       
        Lost,     //挂失-1       
        Normal,   //正常-2      
        Disposed  //注销-3
    }
}
