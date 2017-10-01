using System;
using System.Web;
using System.Configuration;
using System.Xml;
using System.Xml.XmlConfiguration;

namespace Parking.Auxi
{
    public class XMLHelper
    {

        static XMLHelper()
        {
        }

        /// <summary>
        /// 获取root目录下的某个节点的值
        /// </summary>
        /// <param name="root"></param>
        /// <param name="xname"></param>
        /// <returns></returns>
        public static string GetRootNodeValueByXpath(string root, string nodeName)
        {
            Log log = LogFactory.GetLogger("XMLHelper.GetRootNodeValueByXpath");
            string path = "";
            try
            {
               
                path = AppDomain.CurrentDomain.BaseDirectory + @"/System.xml";
              
                //log.Debug("Path- "+path);

                XmlDocument xmlDoc = new XmlDocument();
                XmlReaderSettings setting = new XmlReaderSettings();
                setting.IgnoreComments = true;
                XmlReader reader = XmlReader.Create(path, setting);
                xmlDoc.Load(reader);
                reader.Close();
                string xpath = "//" + root + "//" + nodeName;
                XmlNode node = xmlDoc.SelectSingleNode(xpath);
                if (node != null)
                {
                    return node.InnerXml;
                }
            }
            catch (Exception ex)
            {
                log.Error("path - " + path + " ,root - " + root + " ,nodename - " + nodeName + Environment.NewLine + ex.ToString());
            }
            return null;
        }

        /// <summary>
        /// 获取setting//PLC指定PLC节点下的某个节点值
        /// </summary>
        /// <param name="settingpath">格式：//root//setting</param>
        /// <param name="warehouse">PLC ID="1" 用于匹配 id值</param>
        /// <param name="nodeName">库区内PLC节点下的节点名</param>
        /// <returns></returns>
        public static XmlNode GetPlcNodeByTagName(string settingpath, string warehouse, string nodeName)
        {
            Log log = LogFactory.GetLogger("XMLHelper.GetPlcNodeByTagName");
            string path = "";
            try
            {
                path = AppDomain.CurrentDomain.BaseDirectory + @"/System.xml";
                //log.Debug("Path- " + path);

                XmlDocument xmlDoc = new XmlDocument();
                XmlReaderSettings setting = new XmlReaderSettings();
                setting.IgnoreComments = true;
                XmlReader reader = XmlReader.Create(path, setting);
                xmlDoc.Load(reader);
                reader.Close();
                XmlNode settingNode = xmlDoc.SelectSingleNode(settingpath);
                if (settingpath != null)
                {
                    XmlNodeList nodeList = settingNode.ChildNodes;
                    if (nodeList != null)
                    {
                        XmlNode xnode = GetXmlNodeByAttribute(nodeList, "ID", warehouse);
                        if (xnode != null)
                        {
                            XmlNode element = xnode.SelectSingleNode(nodeName);
                            return element;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("path - " + path + " ,settingpath - " + settingpath + " ,warehouse - " + warehouse + " ,nodename - " + nodeName +
                            Environment.NewLine + ex.ToString());
            }
            return null;
        }

        /// <summary>
        /// 获取setting//PLC(指定)//Halls下指定车厅的节点值
        /// 
        /// </summary>
        /// <param name="settingpath"></param>
        /// <param name="warehouse"></param>
        /// <param name="nodeName"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public static XmlNode GetHallNodeByTageName(string settingpath, string warehouse, string hall, string nodeName)
        {
            Log log = LogFactory.GetLogger("XMLHelper.GetHallNodeByTageName");
            string path = "";
            try
            {
                path = AppDomain.CurrentDomain.BaseDirectory + @"/System.xml";
                //log.Debug("Path- " + path);

                XmlDocument xmlDoc = new XmlDocument();
                XmlReaderSettings setting = new XmlReaderSettings();
                setting.IgnoreComments = true;
                XmlReader reader = XmlReader.Create(path, setting);
                xmlDoc.Load(reader);
                reader.Close();
                XmlNode halls = GetPlcNodeByTagName(settingpath, warehouse, "halls");
                if (halls != null)
                {
                    if (halls.HasChildNodes)
                    {
                        XmlNode hallN = GetXmlNodeHasChildeName(halls.ChildNodes, hall);
                        if (hallN != null)
                        {
                            XmlNode result = hallN.SelectSingleNode(nodeName);
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("path - " + path + " ,settingpath - " + settingpath + " ,warehouse - " + warehouse + " ,Hall - " + hall + " ,nodename - " + nodeName +
                             Environment.NewLine + ex.ToString());
            }
            return null;
        }

        /// <summary>
        /// 查找list节点中存在tagname的节点
        /// </summary>
        /// <param name="nodeList"></param>
        /// <param name="tagname"></param>
        /// <returns></returns>
        public static XmlNode GetXmlNodeByTagName(XmlNodeList nodeList, string tagname)
        {
            foreach (XmlNode node in nodeList)
            {
                if (node.Name == tagname)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// 查找节点清单，找出有对应属性的，且属性值一样的节点
        /// </summary>
        /// <param name="nodeList"></param>
        /// <param name="attribute"></param>
        /// <param name="attrValue"></param>
        /// <returns></returns>
        public static XmlNode GetXmlNodeByAttribute(XmlNodeList nodeList, string attri, string attrValue)
        {
            foreach (XmlNode node in nodeList)
            {
                XmlAttribute attribute = node.Attributes[attri];
                if (attribute != null)
                {
                    if (attribute.InnerText == attrValue)
                    {
                        return node;
                    }
                }
            }
            return null;
        }

        public static string GetXmlValueOfAttribute(XmlNode node, string attri)
        {
            XmlAttribute attribute = node.Attributes[attri];
            if (attribute != null)
            {
                return attribute.InnerText;
            }
            return null;
        }

        /// <summary>
        /// 查询list中的节点的子节点是否包含指定值
        /// </summary>
        /// <param name="nodeList"></param>
        /// <param name="innerText"></param>
        /// <returns>返回list中的对应节点</returns>
        public static XmlNode GetXmlNodeHasChildeName(XmlNodeList nodeList, string innerText)
        {
            foreach (XmlNode node in nodeList)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode xnode in node.ChildNodes)
                    {
                        if (xnode.InnerText == innerText)
                        {
                            return node;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 设置XML值
        /// </summary>
        /// <param name="settingpath"></param>
        /// <param name="innerText"></param>
        public static void SetXmlNodeValue(string xpath, string subnode, string innerText)
        {
            Log log = LogFactory.GetLogger("XMLHelper.SetXmlNodeValue");
            try
            {
                string path = "";
                path = AppDomain.CurrentDomain.BaseDirectory + @"/System.xml";
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                XmlNode node = xmlDoc.SelectSingleNode(xpath);
                if (node != null && node.HasChildNodes)
                {
                    XmlNode xnode = node.SelectSingleNode(subnode);
                    if (xnode != null)
                    {
                        xnode.InnerText = innerText;
                    }
                }
                xmlDoc.Save(path);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 查询Limit值
        /// </summary>
        /// <param name="xpath">\Root\limit</param>
        /// <param name="xnode">code</param>
        /// <returns></returns>
        public static string GetXmlNodeValue(string xpath, string xnode)
        {
            Log log = LogFactory.GetLogger("XMLHelper.GetXmlNodeValue");
            try
            {
                string path = "";
                path = AppDomain.CurrentDomain.BaseDirectory + @"/System.xml";
                XmlDocument xmlDoc = new XmlDocument();
                XmlReaderSettings setting = new XmlReaderSettings();
                setting.IgnoreComments = true;
                XmlReader reader = XmlReader.Create(path, setting);
                xmlDoc.Load(reader);
                reader.Close();

                XmlNode node = xmlDoc.SelectSingleNode(xpath);
                if (node != null && node.HasChildNodes)
                {
                    XmlNode subNode = node.SelectSingleNode(xnode);
                    if (subNode != null)
                    {
                        return subNode.InnerText;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
            return null;
        }

    }
}
