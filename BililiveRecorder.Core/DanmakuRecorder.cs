using BililiveRecorder.Core.Config;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace BililiveRecorder.Core
{
    public class DanmakuRecorder
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public List<MsgTypeEnum> record_filter;
        private static Dictionary<int, DanmakuRecorder> _list = new Dictionary<int, DanmakuRecorder>();
        private StreamMonitor _monitor;
        int roomId = 0;
        RecordedRoom _recordedRoom;
        XmlDocument xml;
        XmlElement root;
        /// <summary>
        /// 注意！这个变量的文件名没有后缀的
        /// </summary>
        string using_fname;

        bool isActive = true;

        int stream_begin;

        //private bool isRecording = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="monitor">对应房间的监视器</param>
        /// <param name="config">设置</param>
        public DanmakuRecorder(StreamMonitor monitor, ConfigV1 config, RecordedRoom recordedRoom)
        {
            //recordedRoom.rec_path
            _recordedRoom = recordedRoom;
            roomId = recordedRoom.RoomId;
            _monitor = monitor;
            if (_list.ContainsKey(roomId))
            {
                logger.Log(LogLevel.Fatal, "!! 另一个弹幕录制模块正在录制这个房间 !!");
                logger.Log(LogLevel.Fatal, "!! 现在，我们将让它保存未完成的任务并关闭它 !!");
                getRecorderbyRoomId(roomId).FinishFile();
            }
            xml = new XmlDocument();
            using_fname = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds.ToString();
            logger.Log(LogLevel.Debug, "弹幕录制暂存为:" + using_fname + ".xml");
            record_filter = new List<MsgTypeEnum>();

            if (config.RecDanmaku) record_filter.Add(MsgTypeEnum.Comment);
            if (config.RecDanmaku_gift) record_filter.Add(MsgTypeEnum.GiftSend);
            if (config.RecDanmaku_guardbuy) record_filter.Add(MsgTypeEnum.GuardBuy);
            if (config.RecDanmaku_unknown) record_filter.Add(MsgTypeEnum.Unknown);
            if (config.RecDanmaku_welguard) record_filter.Add(MsgTypeEnum.WelcomeGuard);
            if (config.RecDanmaku_welcome) record_filter.Add(MsgTypeEnum.Welcome);

            if (record_filter.Count == 0) return;

            #region 弹幕文件的头部
            XmlDeclaration xmldec = xml.CreateXmlDeclaration("1.0", "utf-8", null);
            xml.AppendChild(xmldec);
            root = xml.CreateElement("i");
            xml.AppendChild(root);
            root.AppendChild(node("chatserver", "chat.bilibili.com"));
            root.AppendChild(node("chatid", "000" + roomId));
            root.AppendChild(node("mission", "0"));
            root.AppendChild(node("maxlimit", "2147483647"));
            root.AppendChild(node("state", "0"));
            root.AppendChild(node("real_name", "0"));
            root.AppendChild(node("source", "k-v"));
            #endregion
            //monitor.StreamStarted += _StreamStarted;
            monitor.ReceivedDanmaku += Receiver_ReceivedDanmaku;
            _list.Add(roomId, this);
            stream_begin = DateTimeToUnixTime(DateTime.Now);
            root.AppendChild(node("RECOVER_INFO", "Time_Start", stream_begin.ToString()));
            logger.Log(roomId, LogLevel.Debug, "弹幕录制：直播间开播(@" + stream_begin + ")");
        }
        public XmlElement node(string name,string inner)
        {
            XmlElement ss = xml.CreateElement(name); ss.InnerText = inner;
            return ss;
        }
        public XmlElement node(string name, string att_name,string att_value)
        {
            XmlElement ss = xml.CreateElement(name);
            ss.SetAttribute(att_name, att_value);
            return ss;
        }
        public static int DateTimeToUnixTime(DateTime dateTime)
        {
#pragma warning disable CS0618 // '“TimeZone”已过时:“System.TimeZone has been deprecated.  Please investigate the use of System.TimeZoneInfo instead.”
            return (int)(dateTime - TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1))).TotalSeconds;
#pragma warning restore CS0618 // '“TimeZone”已过时:“System.TimeZone has been deprecated.  Please investigate the use of System.TimeZoneInfo instead.”
        }

        private void Receiver_ReceivedDanmaku(object sender, ReceivedDanmakuArgs e)
        {
            if (!isActive)
            {
                logger.Log(LogLevel.Fatal, "弹幕录制模块的一个对象已经被关闭，却仍然在被调用");
                return;
            }
            //logger.Log(LogLevel.Debug, "收到一条弹幕；" + e.Danmaku.RawData);
            if (_recordedRoom.IsRecording && record_filter.Contains(e.Danmaku.MsgType))//正在录制符合要记录的类型
            {
                //<d p="time, type, fontsize, color, timestamp, pool, userID, danmuID">TEXT</d>
                var obj = JObject.Parse(e.Danmaku.RawData);
                int time_referrence = DateTimeToUnixTime(DateTime.Now);
                switch (e.Danmaku.MsgType)
                {
                    case MsgTypeEnum.Comment:
                        logger.Log(LogLevel.Info, "[弹幕]<" + e.Danmaku.UserName + ">" + e.Danmaku.CommentText);
                        string[] displaydata_ = e.Danmaku.DanmakuDisplayInfo.ToString()
                            .Replace("[", "").Replace("]", "").Replace("\r", "").Replace("\n", "").Replace(" ", "").Split(',');
                        //logger.Log(LogLevel.Info, "[弹幕]<" + e.Danmaku.UserName + ">SENDTIME = " + e.Danmaku.SendTime);
                        StringBuilder sb = new StringBuilder(70);
                        displaydata_[0] = (e.Danmaku.SendTime - stream_begin).ToString();
                        displaydata_[6] = e.Danmaku.UserID.ToString();
                        displaydata_[7] = displaydata_[7].Replace("\"", "");
                        foreach (string arg in displaydata_)
                        {
                            sb.Append(arg + ",");
                        }
                        sb.Remove(sb.Length - 1, 1);
                        var xun = obj["info"][3];
                        string xunz = "False";
                        string streammer = "False";
                        int level = 0;
                        int targetstreamID = 0;
                        if (xun.HasValues)
                        {
                            level = xun[0].ToObject<int>();
                            xunz = xun[1]?.ToObject<string>();
                            streammer = xun[2]?.ToObject<string>();
                            targetstreamID = xun[3].ToObject<int>();
                        }

                        XmlElement ss = node("d", e.Danmaku.CommentText);
                        ss.SetAttribute("p", sb.ToString());
                        ss.SetAttribute("t", e.Danmaku.SendTime.ToString());
                        ss.SetAttribute("un", e.Danmaku.UserName);
                        ss.SetAttribute("cl", e.Danmaku.UserGuardLevel.ToString());
                        ss.SetAttribute("ad", e.Danmaku.IsAdmin.ToString());
                        ss.SetAttribute("vip", e.Danmaku.IsVIP.ToString());
                        ss.SetAttribute("tag", xunz);
                        ss.SetAttribute("tl", level.ToString());
                        ss.SetAttribute("ts", streammer);
                        ss.SetAttribute("tsid", targetstreamID.ToString());
                        root.AppendChild(ss);

                        break;
                    case MsgTypeEnum.GiftSend:
                        logger.Log(LogLevel.Info, "[礼物]<" + e.Danmaku.UserName + ">(" + e.Danmaku.GiftName + ") * " + e.Danmaku.GiftCount);

                        XmlElement el = xml.CreateElement("gift");
                        el.SetAttribute("t", time_referrence.ToString());
                        el.SetAttribute("un", e.Danmaku.UserName);
                        el.SetAttribute("gn", e.Danmaku.GiftName);
                        el.SetAttribute("c", e.Danmaku.GiftCount.ToString());
                        el.SetAttribute("ad", e.Danmaku.IsAdmin.ToString());
                        root.AppendChild(el);

                        break;
                    case MsgTypeEnum.GuardBuy:
                        logger.Log(LogLevel.Info, "[大航海]<" + e.Danmaku.UserName + ">(上船)" + e.Danmaku.GiftCount + "月");

                        XmlElement ep = xml.CreateElement("crew");
                        ep.SetAttribute("t", time_referrence.ToString());
                        ep.SetAttribute("un", e.Danmaku.UserName);
                        ep.SetAttribute("c", e.Danmaku.GiftCount.ToString());
                        ep.SetAttribute("cl", e.Danmaku.UserGuardLevel.ToString());
                        root.AppendChild(ep);

                        break;
                    case MsgTypeEnum.Welcome:
                        logger.Log(LogLevel.Info, "[欢迎]<" + e.Danmaku.UserName + ">(欢迎老爷)");

                        XmlElement ea = xml.CreateElement("vip_enter");
                        ea.SetAttribute("t", time_referrence.ToString());
                        ea.SetAttribute("un", e.Danmaku.UserName);
                        ea.SetAttribute("ad", e.Danmaku.IsAdmin.ToString());
                        ea.SetAttribute("vip", e.Danmaku.IsVIP.ToString());
                        root.AppendChild(ea);

                        break;
                    case MsgTypeEnum.WelcomeGuard:
                        logger.Log(LogLevel.Info, "[欢迎]<" + e.Danmaku.UserName + ">(欢迎船员)");

                        XmlElement eb = xml.CreateElement("crew_enter");
                        eb.SetAttribute("t", time_referrence.ToString());
                        eb.SetAttribute("un", e.Danmaku.UserName);
                        eb.SetAttribute("cl", e.Danmaku.UserGuardLevel.ToString());
                        root.AppendChild(eb);

                        break;
                    case MsgTypeEnum.Unknown:
                        //logger.Log(LogLevel.Debug, "[弹幕](未解析)" + e.Danmaku.RawData);
                        checkUnknownDanmaku(obj);
                        break;
                    default:
                        break;
                }
            }
        }

        public void checkUnknownDanmaku(JObject obj)
        {
            int time_referrence = DateTimeToUnixTime(DateTime.Now);
            string cmd = obj["cmd"]?.ToObject<string>();
            switch (cmd)
            {
                case "ROOM_BLOCK_MSG":
                    string uid = obj["uid"]?.ToObject<string>();
                    string name = obj["uname"]?.ToObject<string>();
                    string operator_ = obj["operator"]?.ToObject<string>();
                    logger.Log(LogLevel.Info, "[管理]" + name + "遭到封禁");

                    XmlElement ea = xml.CreateElement("ban");
                    ea.SetAttribute("t", time_referrence.ToString());
                    ea.SetAttribute("un", name);
                    ea.SetAttribute("uid", uid.ToString());
                    ea.SetAttribute("op", operator_);
                    root.AppendChild(ea);

                    break;
                case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                    string fans = obj["data"]["fans"]?.ToObject<string>();
                    string red_notice = obj["data"]["red_notice"]?.ToObject<string>();
                    logger.Log(LogLevel.Info, "[信息]当前粉丝数：" + fans + "，警告：" + red_notice);

                    XmlElement eb = xml.CreateElement("info");
                    eb.SetAttribute("t", time_referrence.ToString());
                    eb.SetAttribute("fans", fans);
                    eb.SetAttribute("rnt", red_notice);
                    root.AppendChild(eb);

                    break;
                case "ROOM_RANK":
                    string rank_desc = obj["data"]["rank_desc"]?.ToObject<string>();
                    logger.Log(LogLevel.Info, "[信息]直播间当前排名：" + rank_desc);

                    XmlElement ec = xml.CreateElement("rank");
                    ec.SetAttribute("t", time_referrence.ToString());
                    ec.SetAttribute("v", rank_desc);
                    root.AppendChild(ec);

                    break;
            }
        }

        public static DanmakuRecorder getRecorderbyRoomId(int roomid)
        {
            return _list[roomid];
        }

        public void FinishFile()
        {
            if (!isActive)
            {
                logger.Log(LogLevel.Fatal, "弹幕录制模块的一个对象已经被关闭，却仍然在被调用");
                return;
            }
            try
            {
                root.AppendChild(node("RECOVER_INFO", "Time_Stop", DateTimeToUnixTime(DateTime.Now).ToString()));
                XmlElement ver = node("DanmakuRecorder", "version", "3");
                ver.SetAttribute("down_support_to", "3");
                root.AppendChild(ver);
                root.AppendChild(xml.CreateComment("BililiveRecorder | DanmakuRecorder\n" +
                "文件中将包含一些必要的冗余信息以便在时间轴错乱时有机会重新校对时间轴\n" +
                "这些冗余信息不能被其余的弹幕查看软件所理解\n" +
                "如果这个文件无法被正确使用，而里面记录了您重要的录播等弹幕数据，请联系我；\n" +
                "如果你相信软件存在问题，欢迎创建Issue\n\n" +
                "[弹幕部分开发者]\n" +
                "Github: @developer_ken\n" +
                "E-mail: dengbw01@outlook.com\n" +
                "Bilibili: @鸡生蛋蛋生鸡鸡生万物\n" +
                "QQ: 1250542735\n"));

                xml.Save(_recordedRoom.rec_path + ".xml");
                logger.Log(LogLevel.Fatal, "弹幕录制模块的一个实例被结束。");
                logger.Log(LogLevel.Debug, "弹幕文件已保存到：" + _recordedRoom.rec_path + ".xml");
                _list.Remove(roomId);
                isActive = false;
                _monitor.ReceivedDanmaku -= Receiver_ReceivedDanmaku;
            }
            catch (Exception err)
            {
                logger.Log(LogLevel.Error, err.Message);
            }
        }
    }
}
