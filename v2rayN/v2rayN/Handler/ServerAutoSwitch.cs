using DynamicData;
using MaterialDesignThemes.Wpf;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using v2rayN.Mode;
using v2rayN.Resx;
using v2rayN.ViewModels;

namespace v2rayN.Handler
{
    public delegate void SetDefaultServerDelegate(string s);
    public delegate void SetTestResultDelegate(string i, string d, string s);
    public class TestResultItem
    {
        public string indexId { get; set; }
        public long latency { get; set; }
    }
    public class TestSuccessTimeItem
    {
        public string indexId { get; set; }
        public long successtime { get; set; }
    }
    public class latencyCompare : IComparer<TestResultItem>
    {
        public int Compare(TestResultItem x, TestResultItem y)
        {
            return x.latency.CompareTo(y.latency);
        }
     }
    public class ServerAutoSwitch
    {
        private List<TestResultItem> testResultItems= new List<TestResultItem>();
        private List<TestResultItem> testResultItemsMain = new List<TestResultItem>();
        private List<TestSuccessTimeItem> testSuccessTimeItemsMain = new List<TestSuccessTimeItem>();
        private static readonly object objLock = new();
        private bool bStop = false;
        private NoticeHandler? _noticeHandler;
        private Task taskmain = null;
        private SetDefaultServerDelegate setDefaultServerDelegates;
        private SetTestResultDelegate setTestResultDelegates;
        private SetAutoSwitchTogDelegate _setAutoSwitchTog;
        public ServerAutoSwitch()
        {
           
        }
        ~ServerAutoSwitch()
        {
            Stop();
        }
        public void Stop()
        {
            bStop = true;
            if(taskmain!=null)
                taskmain.Wait();
            taskmain = null;
        }
        public void SetDelegate(SetDefaultServerDelegate s = null) {
            this.setDefaultServerDelegates = s;
        }
        public void SetDelegate(SetTestResultDelegate s = null)
        {
            this.setTestResultDelegates = s;
        }
        public void SetDelegate(SetAutoSwitchTogDelegate s = null)
        {
            this._setAutoSwitchTog = s;
        }
        private void UpdateHandler(bool notify, string msg)
        {
        }
        private void UpdateSpeedtestHandler(string id, string dl, string speed)
        {
            lock (objLock)
            {
                int i;
                bool isNumeric = Int32.TryParse(dl, out i);
                if (!isNumeric)
                    return;

                //if (i <= 0 || id == LazyConfig.Instance.GetConfig().indexId)
                //    return;

                testResultItems.Add(new TestResultItem
                {
                    indexId = id,
                    latency = i
                });
                setTestResultDelegates(id, dl, speed);
            }
        }
        private void UpdateSpeedtestHandlerMain(string id, string dl, string speed)
        {
            lock (objLock)
            {
                int i;
                bool isNumeric = Int32.TryParse(dl, out i);
                if (!isNumeric)
                    return;

                //if (i <= 0 || id == LazyConfig.Instance.GetConfig().indexId)
                //    return;

                testResultItemsMain.Add(new TestResultItem
                {
                    indexId = id,
                    latency = i
                });
                setTestResultDelegates(id, dl, speed);
            }
        }
        public void Start()
        {
            if (taskmain != null)
                return;

            var profiles = LazyConfig.Instance.ProfileItemsAutoSwitch();
            if (profiles.Count < 2)
                return;
            bStop = false;

            taskmain = Task.Run(() =>
            {
                _noticeHandler = Locator.Current.GetService<NoticeHandler>();
                DownloadHandle downloadHandle = new();
                long failtimestart = 0;
                long lastlatencytesttime = 0;
                int LastServerSelectMode = -1;
               
                long latencycurrent = 0;
                List<ProfileItem> listprofile = new List<ProfileItem>();
                List<ProfileItem> listprofileMain = new List<ProfileItem>();
                while (!bStop)
                {
                    var _config = LazyConfig.Instance.GetConfig();
                    int iFailTimeMax = LazyConfig.Instance.GetConfig().autoSwitchItem.FailTimeMax;
                    int iTestInterval = iFailTimeMax / 3;
                    int ServerSelectMode = LazyConfig.Instance.GetConfig().autoSwitchItem.ServerSelectMode;
                    int AutoSwitchMode = LazyConfig.Instance.GetConfig().autoSwitchItem.mode;
                    double LatencyLowerRatio = LazyConfig.Instance.GetConfig().autoSwitchItem.LatencyLowerRatio; 

                    Thread.Sleep(iTestInterval * 1000);
                    int res = downloadHandle.RunAvailabilityCheck(null).Result;
                    setTestResultDelegates(_config.indexId, res.ToString(), "");
                    if (res <= 0)
                    {
                        _noticeHandler?.SendMessage("Current server test failed!",true);
                        if (failtimestart == 0)
                            failtimestart = GetTimestamp(DateTime.Now);
                        if (GetTimestamp(DateTime.Now) - failtimestart >= iFailTimeMax || testResultItemsMain?.Count>0 || testResultItems.Count>0)
                        {

                            if (testResultItems.Count == 0 || listprofile.Count == 0 || ServerSelectMode!= LastServerSelectMode)
                            {
                                testResultItems.Clear();
                                listprofile = LazyConfig.Instance.ProfileItemsAutoSwitch();
                                if(AutoSwitchMode==1)
                                {
                                    testResultItemsMain?.Clear();
                                }
                            }

                            if (listprofile.Count >= 2)
                            {

                                if (testResultItems.Count == 0)
                                {
                                    var _coreHandler = new CoreHandler(_config, (bool x, string y) => { });

                                    if (ServerSelectMode == 0 || ServerSelectMode == 1)
                                        new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Tcping, UpdateSpeedtestHandler);
                                    else if (ServerSelectMode == 2)
                                        new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Realping, UpdateSpeedtestHandler);

                                    while (testResultItems.Count < listprofile.Count)
                                    {
                                        Thread.Sleep(20);
                                    }
                                    if (ServerSelectMode == 0)
                                    {
                                        List<TestResultItem> templist = new List<TestResultItem>();

                                        foreach (var item in listprofile)
                                        {
                                            var item2 = testResultItems.Find(x => x.indexId == item.indexId);
                                            if (item2 != null)
                                                templist.Add(item2);
                                        }
                                        testResultItems = templist;
                                    }
                                }
                                if (ServerSelectMode == 0)
                                {
                                    for (int i = testResultItems.Count - 1; i >= 0; i--)
                                    {
                                        if (testResultItems[i].indexId == _config.indexId)
                                            latencycurrent = testResultItems[i].latency;
                                        if (testResultItems[i].latency <= 0 && testResultItems[i].indexId != _config.indexId)
                                            testResultItems.RemoveAt(i);
                                    }
                                    if (testResultItems.Count > 1)
                                    {
                                        int j = testResultItems.FindIndex(x => x.indexId == _config.indexId);
                                        int k = j + 1;
                                        if (k > testResultItems.Count - 1)
                                            k = 0;
                                        setDefaultServerDelegates(testResultItems[k].indexId);
                                        testResultItems.RemoveAt(j);
                                        if (testResultItems.Count <= 1)
                                            testResultItems.Clear();
                                    }
                                    else
                                        testResultItems.Clear();

                                }
                                else if (ServerSelectMode == 1 || ServerSelectMode == 2)
                                {
                                    for (int i = testResultItems.Count - 1; i >= 0; i--)
                                    {
                                        if(testResultItems[i].indexId == _config.indexId)
                                            latencycurrent = testResultItems[i].latency;
                                        if (testResultItems[i].latency <= 0 || testResultItems[i].indexId == _config.indexId)
                                            testResultItems.RemoveAt(i);
                                    }

                                    if (testResultItems.Count > 0)
                                    {
                                        testResultItems.Sort((x, y) => x.latency.CompareTo(y.latency));
                                        if (AutoSwitchMode == 1 && _config.mainServerItems!=null)
                                        {
                                            int maincount = 0;
                                            foreach (var item in _config.mainServerItems)
                                            {
                                                int kk = testResultItems.FindLastIndex(y => y.indexId == item);
                                                if (kk >= 0)
                                                {
                                                    testResultItems.Insert(0, testResultItems[kk]);
                                                    testResultItems.RemoveAt(kk + 1);
                                                    maincount++;
                                                }
                                            }                                           
                                            if(maincount>1)
                                            {
                                                var latencyCompares = new latencyCompare();
                                                testResultItems.Sort(0, maincount, latencyCompares);
                                            }                                              
                                        }
                                        setDefaultServerDelegates(testResultItems[0].indexId);
                                        testResultItems.RemoveAt(0);
                                    }
                                }

                            }
                            else
                            {
                                _setAutoSwitchTog(false);
                                _config.autoSwitchItem.EnableAutoSwitch = false;
                                MessageBox.Show(ResUI.stopSwtichWarn);                           
                            }
                                
                        }
                    }
                    else
                    {
                        if(AutoSwitchMode==1)
                        {
                            if (_config.mainServerItems?.Find(x=>x==_config.indexId)==null)
                            {
                                if(listprofile.Count<=0)
                                {
                                    listprofile = LazyConfig.Instance.ProfileItemsAutoSwitch();

                                }
                                if (listprofileMain.Count <= 0)
                                {
                                    foreach (var item in listprofile)
                                    {
                                        var item2 = _config.mainServerItems?.Find(x => x == item.indexId);
                                        if (item2 != null)
                                            listprofileMain.Add(item);
                                    }
                                }

                                testResultItemsMain?.Clear();

                                var _coreHandler = new CoreHandler(_config, (bool x, string y) => { });

                                if (ServerSelectMode == 0 || ServerSelectMode == 1)
                                    new SpeedtestHandler(_config, _coreHandler, listprofileMain, Mode.ESpeedActionType.Tcping, UpdateSpeedtestHandlerMain);
                                else if (ServerSelectMode == 2)
                                    new SpeedtestHandler(_config, _coreHandler, listprofileMain, Mode.ESpeedActionType.Realping, UpdateSpeedtestHandlerMain);

                                while (testResultItemsMain.Count < listprofileMain.Count)
                                {
                                    Thread.Sleep(20);
                                }

                                testResultItemsMain.Sort((x, y) => x.latency.CompareTo(y.latency));

                                foreach (var item in testResultItemsMain)
                                {
                                    var item2 = testSuccessTimeItemsMain?.Find(x => x.indexId == item.indexId);
                                    if (item.latency==-1)
                                    {                                      
                                        if(item2 != null)
                                            testSuccessTimeItemsMain?.Remove(item2 );
                                    }
                                    else
                                    {
                                        if (item2 != null)
                                        {
                                            if(GetTimestamp(DateTime.Now)-item2.successtime>=iFailTimeMax/2)
                                            {
                                                setDefaultServerDelegates(item2.indexId);
                                                testSuccessTimeItemsMain?.Remove(item2);
                                                _noticeHandler?.SendMessage("Main server test success! switch back", true);
                                                break;
                                            }
                                        }
                                        else
                                            testSuccessTimeItemsMain?.Add(new TestSuccessTimeItem
                                            {
                                                indexId = item.indexId,
                                                successtime = GetTimestamp(DateTime.Now)
                                            });
                                    }
                                }
                            }
                            else
                            {
                                testSuccessTimeItemsMain?.Clear();
                                testResultItemsMain?.Clear();
                                listprofileMain.Clear();
                                failtimestart = 0;
                                testResultItems.Clear();
                                listprofile.Clear();
                            }                                
                        }
                        else if(AutoSwitchMode==2)
                        {
                            if (lastlatencytesttime == 0)
                                lastlatencytesttime = GetTimestamp(DateTime.Now);

                            if (GetTimestamp(DateTime.Now) - lastlatencytesttime >= iFailTimeMax)
                            {
                                if (listprofile.Count <= 0)
                                    listprofile = LazyConfig.Instance.ProfileItemsAutoSwitch();

                                testResultItems.Clear();
                                var _coreHandler = new CoreHandler(_config, (bool x, string y) => { });

                                if (ServerSelectMode == 0 || ServerSelectMode == 1)
                                    new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Tcping, UpdateSpeedtestHandler);
                                else if (ServerSelectMode == 2)
                                    new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Realping, UpdateSpeedtestHandler);

                                while (testResultItems.Count < listprofile.Count)
                                {
                                    Thread.Sleep(20);
                                }

                                testResultItems.Sort((x, y) => x.latency.CompareTo(y.latency));
                                latencycurrent = -1;
                                for (int i = testResultItems.Count - 1; i >= 0; i--)
                                {
                                    if (testResultItems[i].indexId == _config.indexId)
                                        latencycurrent = testResultItems[i].latency;
                                    if (testResultItems[i].latency <= 0 || testResultItems[i].indexId == _config.indexId)
                                        testResultItems.RemoveAt(i);
                                }

                                if (testResultItems.Count > 0 && (latencycurrent <=0 || testResultItems[0].latency <= latencycurrent*(LatencyLowerRatio)))
                                {
                                    setDefaultServerDelegates(testResultItems[0].indexId);
                                    testResultItems.RemoveAt(0);
                                }
                                lastlatencytesttime = GetTimestamp(DateTime.Now);
                            }
                            else
                            {
                                failtimestart = 0;
                                testResultItems.Clear();
                                listprofile.Clear();
                            }
                        }
                        else
                        {
                            failtimestart = 0;
                            testResultItems.Clear();
                            listprofile.Clear();
                        }
                    }
                    LastServerSelectMode = ServerSelectMode;
                }
            });
        }
        public static long GetTimestamp(DateTime dt)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            long timeStamp = (long)(dt- startTime).TotalSeconds;
            return timeStamp;
        }
    }
}
