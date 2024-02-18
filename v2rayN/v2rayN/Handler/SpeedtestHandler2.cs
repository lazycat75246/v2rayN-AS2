using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    public class TestResultItem
    {
        public string indexId { get; set; }
        public long latency { get; set; }
    }

    internal class SpeedtestHandler2
    {
        private Config _config;
        private CoreHandler _coreHandler;
        private List<ServerTestItem> _selecteds;
        private ESpeedActionType _actionType;

        public SpeedtestHandler2(Config config)
        {
            _config = config;
        }

        public void RunPingNew(Config config, CoreHandler coreHandler, List<ProfileItem> selecteds, ESpeedActionType actionType, ref List<TestResultItem> listtestResults)
        {
            _config = config;
            _coreHandler = coreHandler;
            _actionType = actionType;
            _selecteds = new List<ServerTestItem>();
            foreach (var it in selecteds)
            {
                if (it.configType == EConfigType.Custom)
                {
                    continue;
                }
                if (it.port <= 0)
                {
                    continue;
                }
                _selecteds.Add(new ServerTestItem()
                {
                    indexId = it.indexId,
                    address = it.address,
                    port = it.port,
                    configType = it.configType
                });
            }

            listtestResults.Clear();
            int pid = -1;
            try
            {
                if(_actionType== ESpeedActionType.Realping)
                {
                    pid = _coreHandler.LoadCoreConfigSpeedtest(_selecteds);
                    if (pid < 0)
                    {
                        return;
                    }
                }

                DownloadHandle downloadHandle = new DownloadHandle();

                List<Task<TestResultItem>> tasks = new();
                foreach (var it in _selecteds)
                {
                    if (it.configType == EConfigType.Custom)
                    {
                        continue;
                    }
                    ProfileExHandler.Instance.SetTestDelay(it.indexId, "0");
                    tasks.Add(Task.Run(async Task<TestResultItem> () =>
                    {
                        Task.Delay(10);
                        var ret = new TestResultItem
                        {
                            indexId = it.indexId,
                            latency = -1
                        };
                        string output = "";
                        int delay = -1;
                        try
                        {

                            switch (_actionType)
                            {
                                case ESpeedActionType.Tcping:
                                    delay = GetTcpingTime(it.address, it.port);
                                    output = delay.ToString();
                                    break;

                                case ESpeedActionType.Realping:
                                    WebProxy webProxy = new(Global.Loopback, it.port);
                                    output = await GetRealPingTime(downloadHandle, webProxy);
                                    int.TryParse(output, out delay);
                                    break;
                            }

                            ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                            it.delay = delay;
                            ret.latency = delay;
                        }
                        catch (Exception ex)
                        {
                            Logging.SaveLog(ex.Message, ex);
                        }
                        return ret;
                    }));              
                }
                Task.WaitAll(tasks.ToArray());
                foreach (var item in tasks)
                {
                    listtestResults.Add(item.Result);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                if (pid > 0)
                {
                    _coreHandler.CoreStopPid(pid);
                }
                ProfileExHandler.Instance.SaveTo();
            }

            return;
        }

        public async Task<string> GetRealPingTime(DownloadHandle downloadHandle, IWebProxy webProxy)
        {
            int responseTime = await downloadHandle.GetRealPingTime(_config.speedTestItem.speedPingTestUrl, webProxy, 10);
            //string output = Utils.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
            return FormatOut(responseTime, Global.DelayUnit);
        }

        private int GetTcpingTime(string url, int port)
        {
            int responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out IPAddress? ipAddress))
                {
                    IPHostEntry ipHostInfo = System.Net.Dns.GetHostEntry(url);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                Stopwatch timer = new();
                timer.Start();

                IPEndPoint endPoint = new(ipAddress, port);
                using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = clientSocket.BeginConnect(endPoint, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("connect timeout (5s): " + url);
                clientSocket.EndConnect(result);

                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }

        /// <summary>
        /// Ping
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>

        private string FormatOut(object time, string unit)
        {
            //if (time.ToString().Equals("-1"))
            //{
            //    return "Timeout";
            //}
            return $"{time}";
        }
    }
}