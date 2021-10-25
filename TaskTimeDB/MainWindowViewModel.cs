using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

using Prism.Mvvm;
using Reactive.Bindings;

namespace TaskTimeDB
{
    class MainWindowViewModel : BindableBase, IDisposable
    {
        public ReactiveProperty<string> DBFilePath { get; set; }
        public ReactiveProperty<string> LogDirPath { get; set; }
        public ReactiveProperty<DataTable> DB { get; }

        private SQLite sqlite;

        public MainWindowViewModel()
        {
            //
            Config.Load().Wait();
            // 
            /*
            sqlite = new SQLite();
            Disposable.Add(sqlite);
            sqlite.Open();

            sqlite.LoadLogFile("oreore", @"D:\home\csharp\TaskTimerPublish\TaskTimer_work\log\log.20211023.txt").Wait();
            */
            DBFilePath = new ReactiveProperty<string>(Config.DBFilePath, mode: ReactivePropertyMode.DistinctUntilChanged);
            DBFilePath.PropertyChanged += (s, e) => Config.DBFilePath = DBFilePath.Value;
            Disposable.Add(DBFilePath);
            LogDirPath = new ReactiveProperty<string>(Config.LogDirPath, mode: ReactivePropertyMode.DistinctUntilChanged);
            LogDirPath.PropertyChanged += (s, e) => Config.LogDirPath = LogDirPath.Value;
            Disposable.Add(LogDirPath);

            Disposable.Add(Config.config);

            // DB作成
            var tbl = new DataTable();
            for (int i = 0; i<3; i++)
            {
                tbl.Columns.Add(i + "列目");
            }
            for (int i = 0; i < 10; i++)
            {
                var row = tbl.NewRow();
                foreach (DataColumn col in tbl.Columns)
                {
                    row[col] = col.ColumnName + "-" + i + "行目";
                }
                tbl.Rows.Add(row);
            }

            // Reactive設定
            DB = new ReactiveProperty<DataTable>(tbl);
            //DB.Value = tbl;
        }

        // Dispose
        private CompositeDisposable Disposable { get; } = new CompositeDisposable();

        private bool disposedValue = false; // 重複する呼び出しを検出する
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Disposable.Dispose();
                    //Config.Save().Wait();
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
}
