using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    public class SubTask
    {
        public int ID { get; set; }
        public int Warehouse { get; set; }
        public int DeviceCode { get; set; }
        public int Type { get; set; }
        public int Status { get; set; }
        public int SendStatusDetail { get; set; }
        public string CreateDate { get; set; }
        public string SendDtime { get; set; }
        public int HallCode { get; set; }
        public string FromLctAddress { get; set; }
        public string ToLctAddress { get; set; }
        public string ICCardCode { get; set; }
        public int Distance { get; set; }
        public string CarSize { get; set; }
        public int CarWeight { get; set; }
        public int IsComplete { get; set; }
        public string LocSize { get; set; }
    }
}
