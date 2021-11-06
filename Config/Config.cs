using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;

namespace Config
{
    public interface IConfig : IDisposable
    {
        void Load();
        Task LoadAsync();
        void Save();
        Task SaveAsync();

        ReactivePropertySlim<string> DBFilePath { get; set; }
        ReactivePropertySlim<string> LogDirPath { get; set; }
    }

    public class Config : IConfig
    {
        private CompositeDisposable disposables = new CompositeDisposable();
        private string configFilePath;
        public bool PropertyChanged;
        public JsonItem json;

        public Config()
        {
            PropertyChanged = false;
            // パス設定
            configFilePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + @".json";
            // property
            DBFilePath = new ReactivePropertySlim<string>("");
            DBFilePath.Subscribe(x =>
            {
                if (json != null)
                {
                    json.DBFilePath = x;
                    PropertyChanged = true;
                }
            })
            .AddTo(disposables);
            LogDirPath = new ReactivePropertySlim<string>("");
            LogDirPath.Subscribe(x =>
            {
                if (json != null)
                {
                    json.LogDirPath = x;
                    PropertyChanged = true;
                }
            })
            .AddTo(disposables);
        }

        public ReactivePropertySlim<string> DBFilePath { get; set; }
        public ReactivePropertySlim<string> LogDirPath { get; set; }

        public void Load()
        {
            LoadImpl(false).Wait();
        }

        public async Task LoadAsync()
        {
            await LoadImpl(true);
        }

        /** 初回起動用に同期的に動作する
         * 
         */
        private async Task LoadImpl(bool configAwait = true)
        {
            // 設定ロード
            if (File.Exists(configFilePath))
            {
                // ファイルが存在する
                //
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    //Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    //NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
                };
                //
                using (var stream = new FileStream(configFilePath, FileMode.Open, FileAccess.Read))
                {
                    // 呼び出し元でWait()している。ConfigureAwait(false)無しにawaitするとデッドロックで死ぬ。
                    json = await JsonSerializer.DeserializeAsync<JsonItem>(stream, options).ConfigureAwait(configAwait);
                }
            }
            else
            {
                // ファイルが存在しない
                json = new JsonItem
                {
                    DBFilePath = "",
                    LogDirPath = "",
                };
            }
            // property更新
            DBFilePath.Value = json.DBFilePath;
            LogDirPath.Value = json.LogDirPath;
        }

        public void Save()
        {
            SaveImpl(false).Wait();
        }

        public async Task SaveAsync()
        {
            await SaveImpl(true);
        }

        public async Task SaveImpl(bool configAwait = true)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            };
            //
            string jsonStr = JsonSerializer.Serialize(json, options);
            //
            using (var stream = new FileStream(configFilePath, FileMode.Create, FileAccess.Write))
            {
                // 呼び出し元でWait()している。ConfigureAwait(false)無しにawaitするとデッドロックで死ぬ。
                await JsonSerializer.SerializeAsync(stream, json, options).ConfigureAwait(configAwait);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposables.Dispose();
                    if (PropertyChanged)
                    {
                        Save();
                    }
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public class JsonItem
    {
        [JsonPropertyName("db_file_path")]
        public string DBFilePath { get; set; }

        [JsonPropertyName("log_dir_path")]
        public string LogDirPath { get; set; }
    }
}
