using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;


namespace TaskTimeDB
{
    class SQLite : IDisposable
    {
        // DBファイル
        private string outPath;
        private string dbPath;
        // SQLiteインスタンス
        SqliteConnection conn;

        public SQLite()
        {
            outPath = Util.rootPath + @"\";
            dbPath = outPath + @"db.sqlite3";
        }

        public void Dispose()
        {
            if (conn != null)
            {
                conn.Dispose();
            }
        }

        public void Open()
        {
            if (!File.Exists(dbPath))
            {
                // ファイルが存在しないとき新規作成
                InitDb();
            }
            else
            {
                // ファイルが存在するときDB接続
                Connect();
            }
        }

        private void InitDb()
        {
            // DB接続
            Connect();
            // TABLE作成
            CreateDB().Wait();
        }

        private void Connect()
        {
            // DB接続
            conn = new SqliteConnection($"Data Source={dbPath};Foreign Keys=True");
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            conn.Open();
        }

        private async Task<bool> CreateDB()
        {
            /**
             * ・ログファイル保存想定
             * log/
             *      A/
             *          log.YYYYmmdd.txt
             *      B/
             *          log.YYYYmmdd.txt
             *          
             * ・DB構成
             *  persons
             *      person_id   int     PRIMARY
             *      name        string
             *  
             *  tasks
             *      task_id     int     PRIMARY
             *      code        string
             *      name        string
             *  
             *  task_aliases
             *      alias_id    int     PRIMARY
             *      name        string
             *  
             *  subtasks
             *      subtask_id  int     PRIMARY
             *      code        string
             *  
             *  subtask_aliases
             *      alias_id    int     PRIMARY
             *      name        string
             *  
             *  work_times
             *      work_id     int     PRIMARY
             *      task_id     int     foreign key
             *      alias_id    int     foreign key
             *      subtask_id  int     foreign key
             *      subalias_id int     foreign key
             *      date        int
             *      time        int
             */
            // クエリ作成
            var querys = new LinkedList<string>();
            querys.AddLast(@"CREATE TABLE persons(person_id INTEGER PRIMARY KEY, name TEXT);");
            querys.AddLast(@"CREATE TABLE tasks(task_id INTEGER PRIMARY KEY, code TEXT, name TEXT, UNIQUE(code, name));");
            querys.AddLast(@"CREATE TABLE task_aliases(alias_id INTEGER PRIMARY KEY, name TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE subtasks(subtask_id INTEGER PRIMARY KEY, code TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE subtask_aliases(alias_id INTEGER PRIMARY KEY, name TEXT UNIQUE);");
            // work_timesテーブル作成
            var q = new StringBuilder();
            q.Append(@"CREATE TABLE source_infos(");
            q.Append(@"source_id INTEGER PRIMARY KEY");
            q.Append(@", ");
            q.Append(@"person_id INTEGER");
            q.Append(@", ");
            q.Append(@"name TEXT");
            q.Append(@", ");
            q.Append(@"date INTEGER");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(person_id) REFERENCES persons(person_id)");
            q.Append(@");");
            querys.AddLast(q.ToString());
            // work_timesテーブル作成
            q = new StringBuilder();
            q.Append(@"CREATE TABLE work_times(");
            q.Append(@"work_id INTEGER PRIMARY KEY");
            q.Append(@", ");
            q.Append(@"person_id INTEGER");
            q.Append(@", ");
            q.Append(@"task_id INTEGER");
            q.Append(@", ");
            q.Append(@"task_alias_id INTEGER");
            q.Append(@", ");
            q.Append(@"subtask_id INTEGER");
            q.Append(@", ");
            q.Append(@"subtask_alias_id INTEGER");
            q.Append(@", ");
            q.Append(@"source_id INTEGER");
            q.Append(@", ");
            q.Append(@"date INTEGER");
            q.Append(@", ");
            q.Append(@"time INTEGER");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(person_id) REFERENCES persons(person_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(task_id) REFERENCES tasks(task_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(task_alias_id) REFERENCES task_aliases(alias_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(subtask_id) REFERENCES subtasks(subtask_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(subtask_alias_id) REFERENCES subtask_aliases(alias_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(source_id) REFERENCES source_infos(source_id)");
            q.Append(@");");
            querys.AddLast(q.ToString());
            // クエリ実行
            var result = await QueryTransaction(querys);

            return result;
        }


        private async Task<bool> QueryTransaction<T>(T querys)
            where T : IEnumerable<string>
        {
            SqliteTransaction trans = conn.BeginTransaction();

            try
            {
                foreach (var q in querys)
                {
                    using (var command = conn.CreateCommand())
                    {
                        command.Transaction = trans;
                        command.CommandText = q;
                        await command.ExecuteNonQueryAsync();
                        //command.Dispose();
                    }
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                return false;
            }
        }

        private async Task<int> GetLastInsertRowId(SqliteTransaction trans = null)
        {
            // クエリ実行
            using (var command = conn.CreateCommand())
            {
                command.CommandText = "SELECT last_insert_rowid();";
                if (trans != null) command.Transaction = trans;
                var rowid = await command.ExecuteScalarAsync();
                return (int)(long)(rowid);
            }
        }

        public async Task<bool> LoadLogFile(string person, string path)
        {
            SqliteTransaction trans = conn.BeginTransaction();

            try
            {
                // ログファイルを開く
                var log = new LogReader{ Path = path };
                if (!log.Open())
                {
                    return false;
                }
                // ログファイルの内容をDBに展開
                // person_id取得
                var personId = await QueryGetPersonId(trans, person);
                // ログファイルが登録済みかチェック
                var logUpdate = await QueryCheckSource(personId, log);
                bool result;
                switch (logUpdate)
                {
                    case SourceCheck.Update:
                        break;

                    case SourceCheck.NewAdd:
                        // ログ新規追加
                        result = await LoadLogFileAdd(trans, personId, log);
                        break;

                    default:
                        // NoReqは何もしない
                        break;
                }
                using (var command = conn.CreateCommand())
                {
                }

                //foreach (var q in querys)
                //{
                //    using (var command = conn.CreateCommand())
                //    {
                //        command.Transaction = trans;
                //        command.CommandText = q;
                //        await command.ExecuteNonQueryAsync();
                //        command.Dispose();
                //    }
                //}

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                return false;
            }
        }
        private async Task<bool> LoadLogFileAdd(SqliteTransaction trans, int personId, LogReader log)
        {
            // ログファイルの新規登録
            try
            {
                using (var command = conn.CreateCommand())
                {
                }
                while (!log.EOF)
                {
                    var item = log.Get();
                    // SourceInfo登録
                    var sourceId = await QuerySetSourceInfos(trans, personId, log);
                    // タスク登録
                    var taskId = await QueryCheckTasks(trans, item);
                    if (taskId == -1)
                    {
                        taskId = await QuerySetTasks(trans, item);
                    }
                    // タスクAlias登録
                    // サブタスク登録
                    // サブタスクAlias登録
                    // 
                }
                return true;
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QueryGetPersonId(SqliteTransaction trans, string person)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append(@"SELECT person_id FROM persons");
                query.Append($@" WHERE name = '{person}'");
                query.Append(@";");
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = query.ToString();
                    using (var reader = command.ExecuteReader())
                    {
                        var result = new LinkedList<int>();
                        // 結果読み出し
                        while (reader.Read() == true)
                        {
                            result.AddLast((int)(long)reader["person_id"]);
                        }
                        //
                        switch (result.Count)
                        {
                            case 0:
                                return await QuerySetPersonId(trans, person);

                            case 1:
                                // 1個データを取得出来たら正常終了
                                return result.First();

                            default:
                                // 複数個データが取得できるのはありえない
                                throw new Exception($"person name '{person}' is duplicate!");
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QuerySetPersonId(SqliteTransaction trans, string person)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO persons (name) VALUES ('{person}')");
                query.Append(@";");
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = query.ToString();
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がperson_idになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }

        enum SourceCheck
        {
            NoReq,      // 更新不要
            NewAdd,     // 新規登録
            Update      // 要更新
        }
        
        private async Task<SourceCheck> QueryCheckSource(int person_id, LogReader log)
        {
            try
            {
                // クエリ作成
                // 更新日時が同じか新しいログが登録済みなら何もしない
                // 更新日時が古いか登録が無いとき、
                var query = new StringBuilder();
                query.Append(@"SELECT source_id, date FROM source_infos");
                query.Append($@" WHERE person_id = {person_id} AND name = '{log.FileName}'");
                query.Append(@";");
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = query.ToString();
                    using (var reader = command.ExecuteReader())
                    {
                        var result = SourceCheck.NewAdd;
                        // 結果読み出し
                        // 1件しかない前提
                        while (reader.Read() == true)
                        {
                            // 更新日時が同じか新しい場合は更新不要
                            // 更新日時が古い場合は更新する
                            // ここで1件もヒットしないならデータが無いので新規登録
                            if ((int)(long)reader["date"] >= log.LastWriteTime.ToBinary())
                            {
                                result = SourceCheck.NoReq;
                            }
                            else
                            {
                                result = SourceCheck.Update;
                            }
                        }
                        return result;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QuerySetSourceInfos(SqliteTransaction trans, int person_id, LogReader log)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO source_infos (person_id, name, date)");
                query.Append($@" VALUES ('{person_id}', '{log.FileName}', '{log.LastWriteTime.ToBinary()}')");
                query.Append(@";");
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = query.ToString();
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がsource_idになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QueryCheckTasks(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT task_id FROM tasks");
                query.Append($@" WHERE code = '{item.Code}' AND name = '{item.Name}'");
                query.Append(@";");
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = query.ToString();
                    if (trans != null) command.Transaction = trans;
                    var id = await command.ExecuteScalarAsync();
                    if (id != null) return (int)(long)(id);
                    else return -1;
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QuerySetTasks(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO tasks (code, name)");
                query.Append($@" VALUES ('{item.Code}', '{item.Name}')");
                query.Append(@";");
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = query.ToString();
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がsource_idになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }
    }

    class LogType
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public string SubCode { get; set; }
        public string SubAlias { get; set; }
        public string Item { get; set; }
        public int Time { get; set; }           // minute/LSB
    }

    class LogReader : IDisposable
    {
        public string Path { get; set; }
        private StreamReader reader;
        private string buff;
        public DateTime LastWriteTime { get; set; }
        public string FileName { get; set; }

        public LogReader()
        {

        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Close();
            }
        }

        public bool Open()
        {
            if (File.Exists(Path))
            {
                var fi = new FileInfo(Path);
                FileName = fi.Name;
                LastWriteTime = fi.LastWriteTime;
                reader = new StreamReader(Path);
                return true;
            }
            else
            {
                return false;
            }
        }

        static LogType EmptyLog = new LogType { Code=null, Name=null, Alias=null, SubCode=null, SubAlias=null, Item=null, Time=0 };

        public LogType Get()
        {
            if (!reader.EndOfStream)
            {
                buff = reader.ReadLine();
                var match = Util.RegexLog.Match(buff);
                if (match.Success)
                {
                    int min;
                    try
                    {
                        min = int.Parse(match.Groups[7].ToString());
                    }
                    catch
                    {
                        min = 0;
                    }

                    return new LogType {
                        Code = match.Groups[1].ToString(),
                        Name = match.Groups[2].ToString(),
                        Alias = match.Groups[3].ToString(),
                        SubCode = match.Groups[4].ToString(),
                        SubAlias = match.Groups[5].ToString(),
                        Item = match.Groups[6].ToString(),
                        Time = min
                    };
                }
                else
                {
                    return EmptyLog;
                }
            }
            else
            {
                return EmptyLog;
            }
        }

        public bool EOF
        {
            get { return reader.EndOfStream; }
        }
    }
}
