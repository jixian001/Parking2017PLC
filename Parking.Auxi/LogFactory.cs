using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace Parking.Auxi
{
    public class LogFactory
    {
        public static Log GetLogger(Type type)
        {
            Log log = new Log(LogManager.GetLogger(type));
            return log;
        }
        public static Log GetLogger(string str)
        {
            return new Log(LogManager.GetLogger(str));
        }

    }
}
