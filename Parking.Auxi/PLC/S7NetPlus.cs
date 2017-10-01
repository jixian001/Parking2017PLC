using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using S7.Net;
using System.Text.RegularExpressions;

namespace Parking.Auxi
{
    /// <summary>
    /// the library directly from NuGet 
    /// (https://www.nuget.org/packages/S7netplus/).
    /// </summary>
    public class S7NetPlus : IPLC, IDisposable
    {
        private Plc PLCServer;
        private bool isConnect;
        private Log log;

        public S7NetPlus()
        {
            isConnect = false;
            log = LogFactory.GetLogger("S7NetPlus");
        }

        /// <summary>
        /// 不指定PLC型号的，就默认为S7300连接
        /// </summary>
        /// <param name="ipaddrs"></param>
        public S7NetPlus(string ipaddrs) :
            this()
        {
            PLCServer = new Plc(CpuType.S7300, ipaddrs, 0, 2);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plctype">PLC类型：S7200、S7300、S7400、S71200、S71500</param>
        /// <param name="ipAddrs"></param>
        /// <param name="rack">机架号，默认为0</param>
        /// <param name="slot">插槽，为1或2</param>
        public S7NetPlus(string plctype, string ipAddrs, int rack, int slot) :
            this()
        {
            CpuType ctype = CpuType.S71500;
            Enum.TryParse<CpuType>(plctype, out ctype);
            PLCServer = new Plc(ctype, ipAddrs, (short)rack, (short)slot);
        }

        public bool ConnectPLC()
        {
            if (PLCServer != null)
            {
                if (!PLCServer.IsAvailable)
                {
                    log.Error("网络连接出现异常！");
                    return false;
                }

                ErrorCode ecode = PLCServer.Open();
                if (ecode == ErrorCode.NoError)
                {
                    return true;
                }
                else
                {
                    log.Error("PLCServer连接失败，错误代码-" + ecode.ToString());
                }
            }
            else
            {
                log.Error("PLCServer为NULL,没有初始化！");
            }
            return false;
        }

        public bool DisConnectPLC()
        {
            if (PLCServer != null)
            {
                PLCServer.Close();
            }
            return true;
        }

        public bool IsConnected
        {
            get
            {
                if (PLCServer != null)
                {
                    isConnect = PLCServer.IsConnected;
                    return isConnect;
                }
                return false;
            }
            set
            {
                isConnect = value;
            }
        }

        /// <summary>
        /// 读DB块值
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="varType">Bit,Byte,Word,DWord,Int,DInt,Real,String,Timer,Counter</param>
        /// <returns></returns>
        public object ReadData(string itemName, string varType)
        {
            DataType dataType = DataType.DataBlock;
            int DB = 0;
            int startByteAddrs = 0;
            int count = 0;

            //itemName格式 S7:[S7 connection_1]DB1001,INT0,50
            Log log = LogFactory.GetLogger("CSocketPlc.ReadData");
            string[] lstItems = itemName.Split(',');
            if (lstItems.Length == 0)
            {
                log.Info("Item-" + itemName + " 格式不正确！split(',')不出来");
                return null;
            }
            if (lstItems.Length < 3)
            {
                log.Info("Item-" + itemName + " 格式不正确！split(',')出来长度不小于3！");
                return null;
            }
            string head = lstItems[0];
            string[] hArray = head.Split(']');
            if (hArray.Length == 0)
            {
                log.Info("Item-" + itemName + " 格式不正确！Split(']')不出来");
                return null;
            }
            string dtype = hArray.Last();
            if (dtype.Contains("DB"))
            {
                dataType = DataType.DataBlock;
            }
            else
            {
                log.Info("Itme-" + itemName + "格式不正确，不包含DB！");
                return null;
            }
            DB = removeNotNumber(dtype);

            string startAddress = lstItems[1];
            if (!startAddress.ToLower().Contains("int") && !startAddress.ToLower().Contains("b"))
            {
                log.Info("Itme-" + itemName + "格式不正确，不包含INT或B！（INTxx,Bx）");
                return null;
            }
            startByteAddrs = removeNotNumber(startAddress);

            if (!isNumber(lstItems[2]))
            {
                log.Info("Itme-" + itemName + "格式不正确，最后一组不为数字！");
                return null;
            }
            count = Convert.ToInt32(lstItems[2]);

            if (PLCServer != null)
            {
                VarType vtype = VarType.Byte;
                Enum.TryParse<VarType>(varType, out vtype);

                return PLCServer.Read(dataType, DB, startByteAddrs, vtype, count);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 向DB块中写入数据
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public int WriteData(string itemName, object value)
        {
            DataType dataType = DataType.DataBlock;
            int DB = 0;
            int startByteAddrs = 0;

            //itemName格式 S7:[S7 connection_1]DB1001,INT0,50
            Log log = LogFactory.GetLogger("CSocketPlc.WriteData");
            string[] lstItems = itemName.Split(',');
            if (lstItems.Length == 0)
            {
                log.Info("Item-" + itemName + " 格式不正确！split(',')不出来");
                return -1;
            }
            if (lstItems.Length < 3)
            {
                log.Info("Item-" + itemName + " 格式不正确！split(',')出来长度不小于3！");
                return -1;
            }
            string head = lstItems[0];
            string[] hArray = head.Split(']');
            if (hArray.Length == 0)
            {
                log.Info("Item-" + itemName + " 格式不正确！Split(']')不出来");
                return -1;
            }
            string dtype = hArray.Last();
            if (dtype.Contains("DB"))
            {
                dataType = DataType.DataBlock;
            }
            else
            {
                log.Info("Item-" + itemName + "格式不正确，不包含DB！");
                return -1;
            }
            DB = removeNotNumber(dtype);

            string startAddress = lstItems[1];
            if (!startAddress.ToLower().Contains("int"))
            {
                log.Info("Item-" + itemName + "格式不正确，不包含INT！");
                return -1;
            }
            startByteAddrs = removeNotNumber(startAddress);

            if (PLCServer != null)
            {
                byte[] package = null;
                #region
                switch (value.GetType().Name)
                {
                    case "Byte":
                        package = new byte[] { (byte)value };
                        break;
                    case "Int16":
                        package = this.ToByteArray((short)value);
                        break;
                    case "Byte[]":
                        package = (byte[])value;
                        break;
                    case "Int16[]":
                        package = this.ShortArrayToByteArray((short[])value);
                        break;
                    default:
                        return 103;
                }
                #endregion

                ErrorCode ecode = PLCServer.WriteBytes(dataType, DB, startByteAddrs, package);
                if (ecode == ErrorCode.NoError ||
                    ecode == ErrorCode.WriteData ||
                    ecode == ErrorCode.ReadData ||
                    ecode == ErrorCode.SendData)
                {
                    return 1;
                }
                else
                {
                    log.Error("PLCServer向项 " + itemName + " 写入数据失败，错误代码-" + ecode.ToString());
                    //后面如果出现通讯异常，再以ErrorCode 返回值进行分析，再对isconnected进行设置，进行重连工作
                    return -1;
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            DisConnectPLC();
        }

        /// <summary>
        /// 去掉字符串中非数字的
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private int removeNotNumber(string msg)
        {
            string key = Regex.Replace(msg, @"[^\d]*", "");
            if (!string.IsNullOrWhiteSpace(key))
            {
                return Convert.ToInt32(key);
            }
            return -1;
        }

        /// <summary>
        /// 是否为数字
        /// </summary>
        /// <param name="linkNum"></param>
        /// <returns></returns>
        private bool isNumber(string linkNum)
        {
            string pattern = "^[0-9]*[1-9][0-9]*$";
            Regex rx = new Regex(pattern);
            return rx.IsMatch(linkNum);
        }

        /// <summary>
        /// 短整型转化为字节数据
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private byte[] ToByteArray(Int16 value)
        {
            byte[] bytes = new byte[2];
            int x = 2;
            long valLong = (long)((Int16)value);
            for (int cnt = 0; cnt < x; cnt++)
            {
                Int64 x1 = (Int64)Math.Pow(256, (cnt));

                Int64 x3 = (Int64)(valLong / x1);
                bytes[x - cnt - 1] = (byte)(x3 & 255);
                valLong -= bytes[x - cnt - 1] * x1;
            }
            return bytes;
        }

        /// <summary>
        /// 整型数组转化为字节型数组
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private byte[] ShortArrayToByteArray(Int16[] value)
        {
            List<byte> arr = new List<byte>();
            foreach (Int16 val in value)
            {
                arr.AddRange(this.ToByteArray(val));
            }
            return arr.ToArray();
        }

    }

    /// <summary>
    /// 读取的数据单位 byte=1,Int=4
    /// 要返回的数据的类型
    /// </summary>
    public enum DefVarType
    {
        Bit = 0,
        Byte = 1,
        Word = 2,
        DWord = 3,
        Int = 4,
        DInt,
        Real,
        String,
        Timer,
        Counter
    }

}

