using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace Parking.Auxi
{
    public class HttpRequest
    {
        /// <summary>
        /// 发送POST请求
        /// </summary>
        /// <param name="Url">发送的地址</param>
        /// <param name="jsonStr">json字符串</param>
        /// <returns>json字符串</returns>
        public static string HttpPost(string Url, string jsonStr)
        {
            Log log = LogFactory.GetLogger("HttpPost");
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(jsonStr);

                HttpWebRequest request = WebRequest.Create(Url) as HttpWebRequest;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                request.ContentLength = buffer.Length;
                Stream myRequestStream = request.GetRequestStream();
                myRequestStream.Write(buffer, 0, buffer.Length);
                myRequestStream.Close();

                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                Stream myResponseStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.UTF8);
                string retString = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                myResponseStream.Close();

                return retString;

            }
            catch (Exception ex)
            {
                log.Error("URL - "+Url+" ,jsonstr - "+jsonStr+"    异常：" +ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// 发送Get请求
        /// </summary>
        /// <param name="Url">请求地址</param>
        /// <param name="param">请求数据，格式：" 名称1 = 值1 & 名称2 = 值2 </param>
        /// <returns>返回json字符串</returns>
        public static string HttpGet(string Url, string param)
        {
            Log log = LogFactory.GetLogger("HttpGet");
            try
            {
                HttpWebRequest request = WebRequest.Create(Url + (string.IsNullOrEmpty(param) ? "" : ("?" + param))) as HttpWebRequest;
                request.Method = "GET";
                request.ContentType = "application/json;charset=utf-8";

                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                Stream myResponseStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.UTF8);
                string retString = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                myResponseStream.Close();

                return retString;
            }
            catch (Exception ex)
            {
                log.Error("URL - " + Url + " ,param - " + param + "    异常：" + ex.ToString());
                return null;
            }
        }


    }
}
