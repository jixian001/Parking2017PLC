using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Parking.Auxi;
using Newtonsoft.Json.Linq;

namespace Parking2017_PLC
{
    public class CProxy
    {
        private static readonly string baseURL = Properties.Settings.Default.Url;
        private static readonly CProxy myProxy = new CProxy();

        public static CProxy MyProxy
        {
            get
            {
                return myProxy;
            }
        }

        public List<WorkTask> FindQueueList(int warehouse)
        {
            string url = baseURL + "FindQueueList";
            string param = "warehouse=" + warehouse;
            string jsonStr = HttpRequest.HttpGet(url, param);
            if (string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }
            List<WorkTask> queueLst = JsonConvert.DeserializeObject<List<WorkTask>>(jsonStr);
            return queueLst;
        }

        /// <summary>
        /// 处理缓存车位上有车，没有回挪的
        /// </summary>
        /// <param name="warehouse"></param>
        public void DealTempLocOccupy(int warehouse)
        {
            string url = baseURL + "TranferTempLocOccupy";
            string param = "warehouse=" + warehouse;
            string text = HttpRequest.HttpGet(url, param);
        }

        public Device FindDevice(int warehouse,int devicecode)
        {
            string url = baseURL + "FindDevice";
            string param = "warehouse=" + warehouse+"&devicecode="+devicecode;
            string jsonStr = HttpRequest.HttpGet(url, param);
            if (jsonStr == "null" || string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }
            Device smg = JsonConvert.DeserializeObject<Device>(jsonStr);
            return smg;
        }

        public void CreateDeviceTaskByQueue(int qid, int wh, int code)
        {
            var param = new
            {
                queueID = qid,
                warehouse = wh,
                devicecode = code
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "CreateDeviceTaskByQueue";

            HttpRequest.HttpPost(url, jsonStr);
        }

        public ImplementTask FindITask(int tid)
        {
            string url = baseURL + "FindITask";
            string param = "tid=" + tid;
            string jsonStr = HttpRequest.HttpGet(url, param);
            if (jsonStr == "null" || string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }
           
            SubTask itsk = JsonConvert.DeserializeObject<SubTask>(jsonStr);
            ImplementTask subtask = new ImplementTask
            {
                ID = itsk.ID,
                Warehouse = itsk.Warehouse,
                DeviceCode = itsk.DeviceCode,
                Type = (EnmTaskType)itsk.Type,
                Status = (EnmTaskStatus)itsk.Status,
                SendStatusDetail = (EnmTaskStatusDetail)itsk.SendStatusDetail,
                SendDtime = DateTime.Parse(itsk.SendDtime),
                CreateDate = DateTime.Parse(itsk.CreateDate),
                HallCode = itsk.HallCode,
                FromLctAddress = itsk.FromLctAddress,
                ToLctAddress = itsk.ToLctAddress,
                ICCardCode = itsk.ICCardCode,
                Distance = itsk.Distance,
                CarSize = itsk.CarSize,
                CarWeight = itsk.CarWeight,
                IsComplete = itsk.IsComplete,
                LocSize=itsk.LocSize
            };

            return subtask;           
        }

        public string DealTVUnloadTask(int itaskID,int workTaskID)
        {
            var param = new
            {
                taskID=itaskID,
                queueID = workTaskID
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "DealTVUnloadTask";

            return HttpRequest.HttpPost(url, jsonStr);
        }

        public Location FindLocation(string iccode)
        {
            string url = baseURL + "FindLocation";
            string param = "iccode=" + iccode;
            string jsonStr = HttpRequest.HttpGet(url, param);
            if (jsonStr == "null" || string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }
            Location loc = JsonConvert.DeserializeObject<Location>(jsonStr);
            return loc;
        }

        public void DeleteQueue(int qID)
        {
            string url = baseURL + "DeleteQueue";
            string param = "queueID=" + qID;
            string nback = HttpRequest.HttpGet(url, param);
        }

        public void SendHallTelegramAndBuildTV(int nqueueID,int nwarehouse,string nlocaddrs,int ndevicecode)
        {
            var param = new
            {
               queueID = nqueueID,
               warehouse=nwarehouse,
               locaddrs=nlocaddrs,
               devicecode=ndevicecode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "SendHallTelegramAndBuildTV";

            string nback= HttpRequest.HttpPost(url, jsonStr);

        }

        public void AheadTvTelegramAndBuildHall(int nqueueID, int nwarehouse, string nlocaddrs, int ndevicecode)
        {
            var param = new
            {
                queueID = nqueueID,
                warehouse = nwarehouse,
                locaddrs = nlocaddrs,
                devicecode = ndevicecode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "AheadTvTelegramAndBuildHall";

            string nback = HttpRequest.HttpPost(url, jsonStr);

        }

        public List<ImplementTask> FindITaskLst(int nwarehouse)
        {
            string url = baseURL + "FindITaskLst";
            var param = new
            {               
                warehouse = nwarehouse               
            };
            string nback = JsonConvert.SerializeObject(param);

            string jsonStr = HttpRequest.HttpPost(url, nback);
            if (jsonStr == "null" || string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }
            List<SubTask> subtaskLst = JsonConvert.DeserializeObject<List<SubTask>>(jsonStr);

            List<ImplementTask> taskLst = new List<ImplementTask>();
            foreach (SubTask itsk in subtaskLst)
            {
                ImplementTask sub = new ImplementTask
                {
                    ID = itsk.ID,
                    Warehouse = itsk.Warehouse,
                    DeviceCode = itsk.DeviceCode,
                    Type = (EnmTaskType)itsk.Type,
                    Status = (EnmTaskStatus)itsk.Status,
                    SendStatusDetail = (EnmTaskStatusDetail)itsk.SendStatusDetail,
                    SendDtime =DateTime.Parse(itsk.SendDtime),
                    CreateDate = DateTime.Parse(itsk.CreateDate.ToString()),
                    HallCode = itsk.HallCode,
                    FromLctAddress = itsk.FromLctAddress,
                    ToLctAddress = itsk.ToLctAddress,
                    ICCardCode = itsk.ICCardCode,
                    Distance = itsk.Distance,
                    CarSize = itsk.CarSize,
                    CarWeight = itsk.CarWeight,
                    IsComplete = itsk.IsComplete,
                    LocSize = itsk.LocSize
                };
                taskLst.Add(sub);
            }

            return taskLst;
        }

        public string UpdateSendStatusDetail(int itskID,int state)
        {
            var param = new
            {
               itaskID=itskID,
               status=state
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "UpdateSendStatusDetail";

            string nback = HttpRequest.HttpPost(url, jsonStr);

            if (nback == "fail")
            {
                nback= HttpRequest.HttpPost(url, jsonStr);
            }
            return nback;
        }

        public void UnpackUnloadOrder(int tskID)
        {
            string url = baseURL + "UnpackUnloadOrder";
            string param = "taskID=" + tskID;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealICheckCar(int wh,int devicecode,int tid,int distance,string checkcode)
        {
            var param = new
            {
                warehouse=wh,
                hallID=devicecode,
                taskID=tid,
                Distance=distance,
                CheckCode=checkcode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "DealICheckCar";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public string UpdateTaskStatus(int tid,int state)
        {
            var param = new
            {
                taskID = tid,
                taskstatus = state
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "UpdateTaskStatus";
            string nback = HttpRequest.HttpPost(url, jsonStr);
            if (nback == "fail")
            {
                nback = HttpRequest.HttpPost(url, jsonStr);
            }
            return nback;
        }

        public void DealCarLeave(int wh,int nhallID,int ntaskID)
        {
            var param = new
            {
                warehouse=wh,
                hallID=nhallID,
                taskID = ntaskID
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "DealCarLeave";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public void ODealEVUp(int tid)
        {
            string url = baseURL + "ODealEVUp";
            string param = "taskID=" + tid;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealCompleteTask(int tid)
        {
            string url = baseURL + "DealCompleteTask";
            string param = "taskID=" + tid;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealLoadFinishing(int tid,int distance)
        {
            string url = baseURL + "DealLoadFinishing";
            string param = "taskID=" + tid+"&Distance="+distance;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealLoadFinished(int tid)
        {
            string url = baseURL + "DealLoadFinished";
            string param = "taskID=" + tid;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealUnLoadFinishing(int tid)
        {
            string url = baseURL + "DealUnLoadFinishing";
            string param = "taskID=" + tid;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public void DealCarEntrance(int wh,int hall)
        {
            string url = baseURL + "DealCarEntrance";
            string param = "warehouse=" + wh + "&hallID=" + hall;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public List<Device> FindDevicesList(int wh)
        {
            string url = baseURL + "FindDevicesList";
            string param = "warehouse=" + wh;
            string jsonStr = HttpRequest.HttpGet(url, param);

            List<Device> taskLst = JsonConvert.DeserializeObject<List<Device>>(jsonStr);
            return taskLst;
        }

        public List<Alarm> FindAlarmsList(int wh)
        {
            string url = baseURL + "FindAlarmsList";
            string param = "warehouse=" + wh;
            string jsonStr = HttpRequest.HttpGet(url, param);

            List<Alarm> alarmLst = JsonConvert.DeserializeObject<List<Alarm>>(jsonStr);
            return alarmLst;
        }

        public void UpdateDevice(Device smg)
        {
            string jsonStr = JsonConvert.SerializeObject(smg);
            string url = baseURL + "UpdateDevice";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public void UpdateAlarmsList(List<Alarm> needUpdateLst)
        {
            string jsonStr = JsonConvert.SerializeObject(needUpdateLst);
            string url = baseURL + "UpdateAlarmsList";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public void ResetHallOnlyHasTask(int nwarehouse, int ndevicecode)
        {
            var param = new
            {
                warehouse = nwarehouse,
                hallID = ndevicecode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "ResetHallOnlyHasTask";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public void AddTelegramLog(short[] data,int ttype)
        {
            var param = new
            {                
                Type = ttype,
                Telegram = data
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "AddTelegramLog";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        public void ReleaseDeviceTaskIDButNoTask(int wh)
        {
            string url = baseURL + "ReleaseDeviceTaskIDButNoTask";
            string param = "warehouse=" + wh;
            string jsonStr = HttpRequest.HttpGet(url, param);
        }

        public ImplementTask FindITaskBySmg(int warehouse,int devicecode)
        {
            string url = baseURL + "FindITaskBySmg";
            string param = "warehouse=" + warehouse + "&devicecode=" + devicecode;
            string jsonStr = HttpRequest.HttpGet(url, param);
            if (string.IsNullOrEmpty(jsonStr) || jsonStr == "null")
            {
                return null;
            }
            SubTask itsk = JsonConvert.DeserializeObject<SubTask>(jsonStr);
            ImplementTask subtask = new ImplementTask
            {
                ID = itsk.ID,
                Warehouse = itsk.Warehouse,
                DeviceCode = itsk.DeviceCode,
                Type = (EnmTaskType)itsk.Type,
                Status = (EnmTaskStatus)itsk.Status,
                SendStatusDetail = (EnmTaskStatusDetail)itsk.SendStatusDetail,
                SendDtime =DateTime.Parse(itsk.SendDtime),
                CreateDate = DateTime.Parse(itsk.CreateDate),
                HallCode = itsk.HallCode,
                FromLctAddress = itsk.FromLctAddress,
                ToLctAddress = itsk.ToLctAddress,
                ICCardCode = itsk.ICCardCode,
                Distance = itsk.Distance,
                CarSize = itsk.CarSize,
                CarWeight = itsk.CarWeight,
                IsComplete = itsk.IsComplete,
                LocSize = itsk.LocSize
            };

            return subtask;
        }
        
        /// <summary>
        /// 处理车厅车辆跑位
        /// </summary>
        /// <param name="warehouse"></param>
        /// <param name="devicecode"></param>
        public void DealCarTraceOut(int warehouse,int devicecode)
        {
            string url = baseURL + "DealCarTraceOut";
            string param = "warehouse=" + warehouse + "&devicecode=" + devicecode;
            HttpRequest.HttpGet(url, param);
        }

        public void AddSoundNotifi(int warehouse,int devicecode,string soundfile)
        {
            string url = baseURL + "AddSoundNotifi";
            string param = "warehouse=" + warehouse + "&devicecode=" + devicecode + "&soundfile=" + soundfile;
            HttpRequest.HttpGet(url, param);
        }

        public string CreateAvoidTaskByQueue(int queueID)
        {
            string url = baseURL + "CreateAvoidTaskByQueue";
            string param = "queueID=" + queueID;
            string nback= HttpRequest.HttpGet(url, param);

            return "success";
        }

        public bool DealAvoid(int queueID,int warehouse,int devicecode)
        {
            string url = baseURL + "DealAvoid";
            string param = "queueID=" + queueID+ "&warehouse=" + warehouse + "&devicecode=" + devicecode;
            string nback= HttpRequest.HttpGet(url, param);

            JObject jo = (JObject)JsonConvert.DeserializeObject(nback);

            string code = jo["Code"].ToString();
            string msg = jo["Message"].ToString();

            int cd = Convert.ToInt32(code);
            if (cd == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 查询车位信息
        /// </summary>
        /// <param name="warehouse"></param>
        /// <param name="totalspace"></param>
        /// <param name="tempspace"></param>
        /// <returns></returns>
        public bool GetLocInfo(int warehouse,out int totalspace,out int tempspace)
        {
            totalspace = 999;
            tempspace = 999;

            string url = baseURL + "GetLocationsInfo";
            string param = "warehouse=" + warehouse;
            string nback = HttpRequest.HttpGet(url, param);

            JObject jo = (JObject)JsonConvert.DeserializeObject(nback);

            string type= jo["type"].ToString();
            string tl = jo["total"].ToString();
            string tp = jo["temp"].ToString();

            if (type == "1")
            {
                int.TryParse(tl, out totalspace);
                int.TryParse(tp, out tempspace);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 车厅装载时，复检出车辆外形不匹配，要求重新分配的
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="checkcode"></param>
        /// <param name="distance"></param>
        public void ReCheckCarWithLoad(int tid,string checkcode,int distance)
        {
            var param = new
            {
                TaskID = tid,
                Distance = distance,
                CheckCode=checkcode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "ReCheckCarWithLoad";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        /// <summary>
        /// 车位卸载时,车位上有车，则重新分配车位
        /// </summary>
        /// <param name="tid"></param>
        public void DealUnloadButHasCarBlock(int tid)
        {
            var param = new
            {
                TaskID = tid
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "DealUnloadButHasCarBlock";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        /// <summary>
        /// 维护作业队列，是当车厅空闲时，将强制将别的车厅的作业分配给他执行
        /// </summary>
        /// <param name="wh"></param>
        public void MaintainWorkQueue(int wh)
        {
            var param = new
            {
                Warehouse = wh
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "MaintainWorkQueue";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

        /// <summary>
        /// 再次上报（1001，1）是判断，ETV是否移动至车厅门了
        /// </summary>
        /// <param name="wh"></param>
        /// <param name="hallcode"></param>
        public void AHeadMoveEtvToIHall(int wh,int hallcode)
        {
            var param = new
            {
                Warehouse = wh,
                HallCode=hallcode
            };
            string jsonStr = JsonConvert.SerializeObject(param);
            string url = baseURL + "AHeadMoveEtvToIHall";

            string nback = HttpRequest.HttpPost(url, jsonStr);
        }

    }
}
