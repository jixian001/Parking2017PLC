using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ServiceProcess;

namespace Parking2017_PLC
{
    static class Program
    {
        private static int sysModel = Properties.Settings.Default.SystemModel;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (sysModel == 1)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FrmMain());
            }
            else
            {
                //WINDOWS服务启动
                try
                {
                    ServiceBase[] servicesToRun;
                    servicesToRun = new ServiceBase[] { new EqpService() };
                    ServiceBase.Run(servicesToRun);
                }
                catch
                {

                }
            }
        }
    }
}
