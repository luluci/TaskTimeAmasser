using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Resources;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

using WinAPI = Microsoft.WindowsAPICodePack;

namespace TaskTimeAmasser
{

    class MainWindowViewModel : BindableBase, IDisposable
    {
        //
        public ReadOnlyReactivePropertySlim<bool> IsEnableDbCtrl { get; set; }
        public ReactivePropertySlim<string> DBFilePath { get; set; }
        public ReactiveCommand DBFilePathSelect { get; }
        public ReactivePropertySlim<string> DBFileConnectText { get; set; }
        public AsyncReactiveCommand DBFileConnect { get; set; }
        //
        public ReactivePropertySlim<bool> IsEnableRepoCtrl { get; set; }
        public ReadOnlyReactivePropertySlim<bool> IsEnableRepoLoad { get; set; }
        public ReactivePropertySlim<string> LogDirPath { get; set; }
        public ReactiveCommand LogDirPathSelect { get; }
        public ReactivePropertySlim<string> LogDirLoadText { get; set; }
        public AsyncReactiveCommand LogDirLoad { get; set; }
        // Query Preset
        // QueryList
        public List<QueryList> QueryPresetList { get; }
        public ReactivePropertySlim<int> QueryPresetListSelectedIndex { get; set; }
        public AsyncReactiveCommand QueryPresetListExec { get; }
        public List<QueryList> QueryPresetTermList { get; }
        public ReactivePropertySlim<int> QueryPresetTermListSelectedIndex { get; set; }
        public AsyncReactiveCommand QueryPresetTermListExec { get; }
        // Filter全検索
        public ReactiveCollection<string> FilterTaskCode { get; }
        public ReactivePropertySlim<int> FilterTaskCodeSelectIndex { get; set; }
        public ReactivePropertySlim<string> FilterTaskCodeSelectItem { get; set; }
        public ReactivePropertySlim<string> FilterTaskName { get; set; }
        public ReactivePropertySlim<string> FilterTaskAlias { get; set; }
        public ReactivePropertySlim<string> FilterTaskAliasId { get; set; }
        private Dictionary<int,int> FilterTaskAliasIdDict { get; set; } = new Dictionary<int, int>();
        public ReactivePropertySlim<string> FilterPersonId { get; set; }
        private Dictionary<int, int> FilterPersonIdDict { get; set; } = new Dictionary<int, int>();
        public ReactivePropertySlim<string> FilterToolTip { get; set; }
        public AsyncReactiveCommand QueryPresetGetTaskList { get; }
        public AsyncReactiveCommand QueryPresetGetPersonList { get; }
        public AsyncReactiveCommand QueryPresetGetSubTotal { get; }
        public AsyncReactiveCommand QueryPresetGetItemTotal { get; }
        public AsyncReactiveCommand QueryPresetGetPersonTotal { get; }
        public ReactivePropertySlim<string> ExcludeTaskCode { get; set; }
        // 期間集計
        public ReactivePropertySlim<bool> ReflectFilterQueryResultDate { get; set; }
        public ReactivePropertySlim<DateTime> FilterTermBegin { get; set; }
        public ReactivePropertySlim<DateTime> FilterTermEnd { get; set; }
        public ReactivePropertySlim<int> FilterTermUnitSelectIndex { get; set; }
        public AsyncReactiveCommand QueryPresetGetSubTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetItemTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetPersonTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetPersonInfoTerm { get; }
        // Query Manual
        public ReactivePropertySlim<string> QueryText { get; set; }
        public ReactivePropertySlim<bool> EnablePresetUpdateQueryText { get; set; }
        public AsyncReactiveCommand QueryManualExecute { get; }
        // QueryResult領域
        public ReactiveProperty<DataTable> QueryResult { get; }
        public ReactiveCommand<MouseButtonEventArgs> QueryDoubleClick { get; }
        enum QueryResultMode
        {
            Notify,
            TaskList,
            PersonList,
            Other,
        }
        QueryResultMode queryResultDisp = QueryResultMode.Notify;
        DataTable dbNotify;
        // ダイアログ操作
        public ReactivePropertySlim<string> DialogMessage { get; set; }

        private QueryResultResource queryResultResource;
        private Config.IConfig config;
        private Repository.IRepository repository;

        public StackPanel dialog { get; set; } = null;

        public MainWindowViewModel(IContainerProvider diContainer, Config.IConfig config, Repository.IRepository repo)
        {
            this.config = config;
            this.repository = repo;

            // ResourceDictionary取得
            ResourceDictionary dic = new ResourceDictionary
            {
                Source = new Uri("/TaskTimeAmasser;component/Resources/GUIDictionary.xaml", UriKind.Relative)
            };
            // ResourceDictionary
            queryResultResource = new QueryResultResource();

            // Configロード
            config.Load();
            config.AddTo(Disposables);
            // GUI初期化
            // DBFilePath設定
            // DB接続/切断ボタン表示テキスト
            DBFileConnectText = new ReactivePropertySlim<string>("DB接続");
            // DBファイル指定有効無効
            IsEnableDbCtrl = repository.IsConnect
                .Inverse()
                .ToReadOnlyReactivePropertySlim();
            IsEnableDbCtrl
                .Subscribe((x) =>
                {
                    if (x)
                    {
                        DBFileConnectText.Value = "DB接続";
                    }
                    else
                    {
                        DBFileConnectText.Value = "DB切断";
                    }
                })
                .AddTo(Disposables);
            // DBファイルパス
            DBFilePath = config.DBFilePath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
            // DBファイル指定ダイアログボタンコマンド
            DBFilePathSelect = IsEnableDbCtrl.ToReactiveCommand();
            DBFilePathSelect
                .Subscribe(_ => {
                    var result = FileSelectDialog(DBFilePath.Value);
                    if (!(result is null))
                    {
                        DBFilePath.Value = result;
                    }
                })
                .AddTo(Disposables);
            // DB接続/切断ボタンコマンド
            //DBFileConnect = new AsyncReactiveCommand();
            DBFileConnect = repository.IsLoading.Inverse().ToAsyncReactiveCommand();
            DBFileConnect
                .WithSubscribe(async () => {
                    if (repository.IsConnect.Value)
                    {
                        await repository.Close();
                        UpdateQueryResultNotify("DB切断しました");
                    }
                    else
                    {
                        DialogMessage.Value = "Connecting DB ...";
                        var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                        {
                            //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe START");
                            var connResult = await repository.Connect(config.DBFilePath.Value);
                            // Message通知
                            if (repository.IsConnect.Value)
                            {
                                bool r;
                                // タスクコードリスト取得
                                r = await repository.UpdateTaskCodeList();
                                if (r)
                                {
                                    UpdateQueryInfo();
                                }
                                // レコード日時最小最大を取得
                                // ここでは必ず設定する
                                // ReflectFilterQueryResultDate.Value
                                await UpdateDateRange();
                                UpdateQueryResultNotify("DB接続しました");
                            }
                            else
                            {
                                UpdateQueryResultNotify($"DB接続失敗しました:\r\n{repository.ErrorMessage}");
                            }
                            //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe END");
                            args.Session.Close(false);
                        });
                    }
                })
                .AddTo(Disposables);
            // LogDir設定
            // Logファイルロードボタン表示テキスト
            LogDirLoadText = new ReactivePropertySlim<string>("ログファイル取り込み");
            // Logファイルロード有効無効
            IsEnableRepoLoad = repository.IsConnect
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            // Logファイル設定有効無効
            IsEnableRepoCtrl = new ReactivePropertySlim<bool>(true);
            // Logファイル保存ディレクトリパス
            LogDirPath = config.LogDirPath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
            // Logファイル保存ディレクトリ選択ダイアログボタンコマンド
            LogDirPathSelect = IsEnableRepoCtrl.ToReactiveCommand();
            LogDirPathSelect
                .Subscribe(_ => {
                    var result = DirSelectDialog(LogDirPath.Value);
                    if (!(result is null))
                    {
                        LogDirPath.Value = result;
                    }
                })
                .AddTo(Disposables);
            // Logファイルロード開始ボタンコマンド
            LogDirLoad = IsEnableRepoLoad
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    IsEnableRepoCtrl.Value = false;
                    DialogMessage.Value = "Loading LogFiles ...";
                    var dlgresult = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var result = await repository.Load(LogDirPath.Value);
                        if (result)
                        {
                            bool r;
                            // タスクコードリスト更新
                            r = await repository.UpdateTaskCodeList();
                            if (r)
                            {
                                UpdateQueryInfo();
                            }
                            // 日時範囲更新
                            // レコード内日時範囲反映設定が有効であれば更新する
                            if (ReflectFilterQueryResultDate.Value)
                            {
                                await UpdateDateRange();
                            }
                            UpdateQueryResultNotify("Logファイル取り込み正常終了");
                        }
                        else
                        {
                            UpdateQueryResultNotify($"Logファイル取り込み失敗:\r\n{repository.ErrorMessage}");
                        }
                        args.Session.Close(false);
                    });
                    IsEnableRepoCtrl.Value = true;
                })
                .AddTo(Disposables);
            // Query Preset
            // Queryリスト
            QueryPresetList = new List<QueryList>
            {
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalSub"].ToString(),
                    Exec = GetSelectSubTotal,
                },
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalSubItem"].ToString(),
                    Exec = GetSelectItemTotal,
                },
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalPerson"].ToString(),
                    Exec = GetSelectPersonTotal,
                },
            };
            QueryPresetTermList = new List<QueryList>
            {
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalSub"].ToString(),
                    Exec = GetSelectSubTotalTerm,
                },
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalSubItem"].ToString(),
                    Exec = GetSelectItemTotalTerm,
                },
                new QueryList
                {
                    Name = dic["GuiDispQueryListTotalPerson"].ToString(),
                    Exec = GetSelectPersonTotalTerm,
                },
            };
            QueryPresetListSelectedIndex = new ReactivePropertySlim<int>(0);
            QueryPresetListSelectedIndex
                .AddTo(Disposables);
            QueryPresetListExec = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    // indexチェック
                    var idx = QueryPresetListSelectedIndex.Value;
                    if (idx < 0) return;
                    // query実行
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await QueryPresetList[idx].Exec();
                        });
                        UpdateDbView(r, QueryResultMode.TaskList);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetTermListSelectedIndex = new ReactivePropertySlim<int>(0);
            QueryPresetTermListSelectedIndex
                .AddTo(Disposables);
            QueryPresetTermListExec = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    // indexチェック
                    var idx = QueryPresetTermListSelectedIndex.Value;
                    if (idx < 0) return;
                    // query実行
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await QueryPresetTermList[idx].Exec();
                        });
                        UpdateDbView(r, QueryResultMode.TaskList);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            // 個別実行ボタン
            FilterTaskCode = new ReactiveCollection<string>
            {
                "<指定なし>"
            };
            FilterTaskCode
                .AddTo(Disposables);
            FilterTaskCodeSelectItem = new ReactivePropertySlim<string>("");
            FilterTaskCodeSelectItem
                .AddTo(Disposables);
            FilterTaskCodeSelectIndex = new ReactivePropertySlim<int>(0);
            FilterTaskCodeSelectIndex
                .AddTo(Disposables);
            FilterTaskName = new ReactivePropertySlim<string>("");
            FilterTaskName
                .AddTo(Disposables);
            FilterTaskAlias = new ReactivePropertySlim<string>("");
            FilterTaskAlias
                .AddTo(Disposables);
            FilterTaskAliasId = new ReactivePropertySlim<string>("");
            FilterTaskAliasId
                .Subscribe(x =>
                {
                    MakeTaskAliasIdFilter(x);
                })
                .AddTo(Disposables);
            FilterPersonId = new ReactivePropertySlim<string>("");
            FilterPersonId
                .Subscribe(x =>
                {
                    MakePersonIdFilter(x);
                })
                .AddTo(Disposables);
            FilterToolTip = new ReactivePropertySlim<string>("*       任意の0文字以上の文字列\r\n?       任意の1文字\r\n[abc]  a or b or cのいずれかに一致\r\n[a-d]  aからdまでにいずれかに一致");
            FilterToolTip
                .AddTo(Disposables);
            // DBファイルパス
            ExcludeTaskCode = config.QueryExcludeTaskCode
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
            //QueryPresetGetTaskList = new AsyncReactiveCommand();
            QueryPresetGetTaskList = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetTaskList();
                        });
                        UpdateDbView(r, QueryResultMode.TaskList);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonList = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetPersonList();
                        });
                        UpdateDbView(r, QueryResultMode.PersonList);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetSubTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var qr = await GetSelectSubTotal();
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetItemTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var qr = await GetSelectItemTotal();
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var qr = await GetSelectPersonTotal();
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            ReflectFilterQueryResultDate = new ReactivePropertySlim<bool>(true);
            ReflectFilterQueryResultDate
                .AddTo(Disposables);
            FilterTermBegin = new ReactivePropertySlim<DateTime>(DateTime.Now);
            FilterTermBegin
                .AddTo(Disposables);
            FilterTermEnd = new ReactivePropertySlim<DateTime>(DateTime.Now);
            FilterTermEnd
                .AddTo(Disposables);
            FilterTermUnitSelectIndex = new ReactivePropertySlim<int>(1);
            FilterTermUnitSelectIndex
                .AddTo(Disposables);
            QueryPresetGetSubTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetSelectSubTotalTerm();
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetItemTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetSelectItemTotalTerm();
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetSelectPersonTotalTerm();
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonInfoTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetSelectPersonInfoTerm();
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            // Query Manual
            QueryText = new ReactivePropertySlim<string>("");
            EnablePresetUpdateQueryText = new ReactivePropertySlim<bool>(true);
            QueryManualExecute = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        if (QueryText.Value.Length > 0)
                        {
                            var r = await ExecuteQuery(QueryText.Value);
                            UpdateDbView(r, QueryResultMode.Other);
                        }
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            //
            DialogMessage = new ReactivePropertySlim<string>("");
            DialogMessage.AddTo(Disposables);

            // QueryResult領域
            // メッセージ表示用DataTable作成
            dbNotify = new DataTable();
            dbNotify.Columns.Add("Message");
            {
                var row = dbNotify.NewRow();
                row[0] = "<DB未接続>";
                dbNotify.Rows.Add(row);
            }
            // Reactive設定
            QueryResult = new ReactiveProperty<DataTable>(dbNotify);
            QueryDoubleClick = new ReactiveCommand<MouseButtonEventArgs>();
            QueryDoubleClick
                .WithSubscribe(OnDoubleClickQueryResult)
                .AddTo(Disposables);
        }

        private void OnDoubleClickQueryResult(MouseButtonEventArgs e)
        {
            switch (queryResultDisp)
            {
                case QueryResultMode.TaskList:
                    OnDoubleClickQueryResultTaskList(e);
                    break;
                case QueryResultMode.PersonList:
                    OnDoubleClickQueryResultPersonList(e);
                    break;
            }
        }
        private void OnDoubleClickQueryResultTaskList(MouseButtonEventArgs e)
        {
            var elem = e.MouseDevice.DirectlyOver as FrameworkElement;
            if (elem != null)
            {
                DataGridCell cell = elem.Parent as DataGridCell;
                if (cell == null)
                {
                    // ParentでDataGridCellが拾えなかった時はTemplatedParentを参照
                    // （Borderをダブルクリックした時）
                    cell = elem.TemplatedParent as DataGridCell;
                }
                if (cell != null)
                {
                    // ここでcellの内容を処理
                    // （cell.DataContextにバインドされたものが入っているかと思います）
                    var row = ((DataRowView)cell.DataContext).Row;
                    //var rowidx = QueryResult.Value.Rows.IndexOf(row);
                    var colidx = cell.Column.DisplayIndex;
                    //MessageBox.Show($"({rowidx}, {cell.Column.DisplayIndex})");
                    var data = row.Field<string>(colidx);
                    // 選択した内容を転送
                    switch (colidx)
                    {
                        case 0:
                            // TaskCode
                            FilterTaskCodeSelectItem.Value = data;
                            break;
                        case 1:
                            // TaskName
                            FilterTaskName.Value = data;
                            break;
                        case 2:
                            // TaskAlias
                            FilterTaskAlias.Value = data;
                            break;
                        case 3:
                            // TaskAliasId
                            if (int.TryParse(data, out int val))
                            {
                                AddTaskAliasIdFilter(val);
                            }
                            break;
                    }
                }
            }
        }
        private void OnDoubleClickQueryResultPersonList(MouseButtonEventArgs e)
        {
            var elem = e.MouseDevice.DirectlyOver as FrameworkElement;
            if (elem != null)
            {
                DataGridCell cell = elem.Parent as DataGridCell;
                if (cell == null)
                {
                    // ParentでDataGridCellが拾えなかった時はTemplatedParentを参照
                    // （Borderをダブルクリックした時）
                    cell = elem.TemplatedParent as DataGridCell;
                }
                if (cell != null)
                {
                    // ここでcellの内容を処理
                    // （cell.DataContextにバインドされたものが入っているかと思います）
                    var row = ((DataRowView)cell.DataContext).Row;
                    //var rowidx = QueryResult.Value.Rows.IndexOf(row);
                    var colidx = cell.Column.DisplayIndex;
                    //MessageBox.Show($"({rowidx}, {cell.Column.DisplayIndex})");
                    // 現状ではIDだけ取得する
                    //var data = row.Field<string>(colidx);
                    var data = row.Field<string>(0);
                    // 選択した内容を転送
                    switch (colidx)
                    {
                        case 0:
                        case 1:
                            // PersonId
                            if (int.TryParse(data, out int val))
                            {
                                AddPersonIdFilter(val);
                            }
                            break;
                            /*
                        case 1:
                            // PersonName(未実装)
                            FilterTaskName.Value = data;
                            break;
                            */
                    }
                }
            }
        }

        private void UpdateQueryResultNotify(string msg)
        {
            dbNotify.Rows[0][0] = msg;
            queryResultDisp = QueryResultMode.Notify;
            QueryResult.Value = dbNotify;
        }

        private void UpdateQueryInfo()
        {
            // フィルタ用タスクコードリスト更新
            FilterTaskCode.Clear();
            FilterTaskCode.Add("<指定なし>");
            foreach (var item in repository.TaskCodeList)
            {
                FilterTaskCode.Add(item.code);
            }
            FilterTaskCodeSelectIndex.Value = 0;
        }

        private async Task UpdateDateRange()
        {
            var filter = MakeQueryFilterTask();
            var query = SqlQuery.MakeQuerySelectDateRange(queryResultResource, filter);
            var dateResult = await repository.UpdateDateRange(query);
            if (dateResult)
            {
                // 集計用日時更新
                if (repository.DateRange.begin != 0 && repository.DateRange.end != 0)
                {
                    FilterTermBegin.Value = DateTime.FromBinary(repository.DateRange.begin);
                    FilterTermEnd.Value = DateTime.FromBinary(repository.DateRange.end);
                }
            }
        }

        private async Task<bool> GetTaskList()
        {
            var filter = MakeQueryFilterTask();
            var q = SqlQuery.MakeQuerySelectTaskList(queryResultResource, filter);
            return await ExecuteQuery(q);
        }

        private async Task<bool> GetPersonList()
        {
            var filter = new QueryFilterTask();
            var q = SqlQuery.MakeQuerySelectPersonList(queryResultResource, filter);
            return await ExecuteQuery(q);
        }

        static readonly Regex reIdFilterList = new Regex(@"(\d+),?", RegexOptions.Compiled);
        private bool inAddTaskAliasIdFilter = false;
        private void MakeTaskAliasIdFilter(string text)
        {
            if (!inAddTaskAliasIdFilter)
            {
                var str = MakeIdFilter(text, FilterTaskAliasIdDict);
                // GUI更新
                inAddTaskAliasIdFilter = true;
                FilterTaskAliasId.Value = str;
                inAddTaskAliasIdFilter = false;
            }
        }
        private void AddTaskAliasIdFilter(int id)
        {
            var result = AddIdFilter(id, FilterTaskAliasId.Value, FilterTaskAliasIdDict);
            if (!(result is null))
            {
                // GUI更新
                inAddTaskAliasIdFilter = true;
                FilterTaskAliasId.Value = result;
                inAddTaskAliasIdFilter = false;
            }
        }
        private bool inAddPersonIdFilter = false;
        private void MakePersonIdFilter(string text)
        {
            if (!inAddPersonIdFilter)
            {
                var str = MakeIdFilter(text, FilterPersonIdDict);
                // GUI更新
                inAddPersonIdFilter = true;
                FilterPersonId.Value = str;
                inAddPersonIdFilter = false;
            }
        }
        private void AddPersonIdFilter(int id)
        {
            var result = AddIdFilter(id, FilterPersonId.Value, FilterPersonIdDict);
            if (!(result is null))
            {
                // GUI更新
                inAddPersonIdFilter = true;
                FilterPersonId.Value = result;
                inAddPersonIdFilter = false;
            }
        }
        private string MakeIdFilter(string text, Dictionary<int, int> dict)
        {
            // textからコンマ区切りのIdFilterを作成する
            // Dictionary上に生成すると同時に文字列化して返す
            dict.Clear();
            // テキストチェック
            var comma = "";
            var str = new StringBuilder();
            var matches = reIdFilterList.Matches(text);
            for (var i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                str.Append($"{comma}{m.Groups[1]}");
                comma = ",";
                if (int.TryParse(m.Groups[1].ToString(), out int val))
                {
                    if (!dict.ContainsKey(val))
                    {
                        dict.Add(val, 1);
                    }
                }
            }
            return str.ToString();
        }
        private string AddIdFilter(int id, string text, Dictionary<int, int> dict)
        {
            if (!dict.ContainsKey(id))
            {
                // 新しいIDが指定された場合
                // 辞書に登録
                dict.Add(id, 1);
                // フィルタテキスト更新
                if (text.Length == 0)
                {
                    text = $"{id}";
                }
                else
                {
                    text = $"{text},{id}";
                }
                return text;
            }
            return null;
        }

        private QueryFilterTask MakeQueryFilterTask()
        {
            // タスクフィルタ作成
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                TaskAliasId = FilterTaskAliasId.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
                //ExcludeSubTaskCode = new List<string> { "CodeB", "CodeF" },
                PersonId = FilterPersonId.Value,
            };
            filter.Init();
            return filter;
        }

        private QueryFilterTerm MakeQueryFilterTerm()
        {
            // 期間フィルタ作成
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return term;
        }



        private async Task<bool> GetSelectSubTotal()
        {
            // フィルタオブジェクト作成
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectSubTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }

        private async Task<bool> GetSelectSubTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectSubTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }
        

        private async Task<bool> GetSelectItemTotal()
        {
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectItemTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }

        private async Task<bool> GetSelectItemTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectItemTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }


        private async Task<bool> GetSelectPersonInfoTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectPersonInfoTerm(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }


        private async Task<bool> GetSelectPersonTotal()
        {
            // フィルタオブジェクト作成
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectPersonTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }

        private async Task<bool> GetSelectPersonTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            var query = SqlQuery.MakeQuerySelectPersonTotal(queryResultResource, filter, term);
            return await ExecuteQuery(query);
        }



        private async Task<bool> ExecuteQuery(string query)
        {
            // クエリ実行前処理
            if (EnablePresetUpdateQueryText.Value)
            {
                QueryText.Value = query;
            }
            // クエリ実行
            return await repository.QueryExecute(query);
        }
        private void UpdateDbView(bool executeResult, QueryResultMode mode)
        {
            if (executeResult)
            {
                // 結果反映
                queryResultDisp = mode;
                QueryResult.Value = repository.QueryResult;
            }
            else
            {
                UpdateQueryResultNotify(repository.ErrorMessage);
            }
            /*
            DB.Value.Clear();
            // Column作成
            foreach (DataColumn col in repository.QueryResult.Columns)
            {
                DB.Value.Columns.Add(col.ColumnName);
            }
            // Row作成
            foreach (DataRow row in repository.QueryResult.Rows)
            {
                var new_row = DB.Value.NewRow();
                foreach (DataColumn col in repository.QueryResult.Columns)
                {
                }
            }
            */
        }

        private string DirSelectDialog(string initDir)
        {
            string result = null;
            var dlg = new WinAPI::Dialogs.CommonOpenFileDialog
            {
                // フォルダ選択ダイアログ（falseにするとファイル選択ダイアログ）
                IsFolderPicker = true,
                // タイトル
                Title = "フォルダを選択してください",
                // 初期ディレクトリ
                InitialDirectory = initDir
            };

            if (dlg.ShowDialog() == WinAPI::Dialogs.CommonFileDialogResult.Ok)
            {
                result = dlg.FileName;
            }

            return result;
        }

        private string FileSelectDialog(string initDir)
        {
            string result = null;
            var dlg = new WinAPI::Dialogs.CommonOpenFileDialog
            {
                // フォルダ選択ダイアログ（falseにするとファイル選択ダイアログ）
                IsFolderPicker = false,
                // タイトル
                Title = "ファイルを選択してください",
                // 初期ディレクトリ
                InitialDirectory = initDir
            };
            
            if (dlg.ShowDialog() == WinAPI::Dialogs.CommonFileDialogResult.Ok)
            {
                result = dlg.FileName;
            }

            return result;
        }


        // Dispose
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();

        private bool disposedValue = false; // 重複する呼び出しを検出する
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Disposables.Dispose();
                }

                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
        }
    }

    class QueryList
    {
        public string Name { get; set; } = string.Empty;
        // dummyで初期化しておく
        public Func<Task<bool>> Exec = async () => await Task.Run(() => false);
    }
}
