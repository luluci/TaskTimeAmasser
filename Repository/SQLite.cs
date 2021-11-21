using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;


namespace Repository
{
    class SQLite : IDisposable
    {
        // DBファイル
        private string dbPath;
        // SQLiteインスタンス
        private SqliteConnection conn;
        // ErrorMessage
        public string LastQuery { get; set; } = "";
        public string LastErrorMessage { get; set; } = "";

        public SQLite()
        {
        }

        public void Dispose()
        {
            Close();
        }

        public async Task<bool> Open(string dbPath)
        {
            bool result;
            // 一応記憶
            this.dbPath = dbPath;
            // DB接続
            if (!File.Exists(dbPath))
            {
                // ファイルが存在しないとき新規作成
                result = await InitDb();
            }
            else
            {
                // ファイルが存在するときDB接続
                result = Connect();
            }

            return result;
        }

        public void Close()
        {
            if (conn != null)
            {
                conn.Dispose();
                conn = null;
            }
        }

        private async Task<bool> InitDb()
        {
            bool result;
            // DB接続
            result = Connect();
            if (result)
            {
                // TABLE作成
                result = await CreateDB();
            }
            return result;
        }

        private bool Connect()
        {
            try
            {
                // DB接続
                conn = new SqliteConnection($"Data Source={dbPath};Foreign Keys=True");
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                conn.Open();

                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"DB Connect failed: {ex.Message}";
                return false;
            }
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
             *      person_id           int     PRIMARY
             *      person_name         string
             *  
             *  tasks
             *      task_id             int     PRIMARY
             *      task_code           string
             *      task_name           string
             *  
             *  task_aliases
             *      task_alias_id       int     PRIMARY
             *      task_alias_name     string
             *  
             *  subtasks
             *      subtask_id          int     PRIMARY
             *      subtask_code        string
             *  
             *  subtask_aliases
             *      subtask_alias_id    int     PRIMARY
             *      subtask_alias_name  string
             *  
             *  items
             *      item_id             int     PRIMARY
             *      item_name           string
             *  
             *  item_aliases
             *      item_alias_id       int     PRIMARY
             *      item_alias_name     string
             *  
             *  source_infos
             *      source_id               int     PRIMARY
             *      person_id               int     foreign key
             *      source_name             string
             *      source_lastmodify_date  int
             *  
             *  work_times
             *      work_id           int     PRIMARY
             *      person_id         int     foreign key
             *      task_id           int     foreign key
             *      task_alias_id     int     foreign key
             *      subtask_id        int     foreign key
             *      subtask_alias_id  int     foreign key
             *      item_id           int     foreign key
             *      item_alias_id     int     foreign key
             *      source_id         int     foreign key
             *      date              int
             *      year              int
             *      month             int
             *      day               int
             *      time              int
             */
            // クエリ作成
            var querys = new LinkedList<string>();
            querys.AddLast(@"CREATE TABLE persons(person_id INTEGER PRIMARY KEY, person_name TEXT);");
            querys.AddLast(@"CREATE TABLE tasks(task_id INTEGER PRIMARY KEY, task_code TEXT, task_name TEXT, UNIQUE(task_code, task_name));");
            querys.AddLast(@"CREATE TABLE task_aliases(task_alias_id INTEGER PRIMARY KEY, task_alias_name TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE subtasks(subtask_id INTEGER PRIMARY KEY, subtask_code TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE subtask_aliases(subtask_alias_id INTEGER PRIMARY KEY, subtask_alias_name TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE items(item_id INTEGER PRIMARY KEY, item_name TEXT UNIQUE);");
            querys.AddLast(@"CREATE TABLE item_aliases(item_alias_id INTEGER PRIMARY KEY, item_alias_name TEXT UNIQUE);");
            // source_infosテーブル作成
            var q = new StringBuilder();
            q.Append(@"CREATE TABLE source_infos(");
            q.Append(@"source_id INTEGER PRIMARY KEY");
            q.Append(@", ");
            q.Append(@"person_id INTEGER");
            q.Append(@", ");
            q.Append(@"source_name TEXT");
            q.Append(@", ");
            q.Append(@"source_lastmodify_date INTEGER");
            q.Append(@", ");
            q.Append(@"UNIQUE(source_name, source_lastmodify_date)");
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
            q.Append(@"item_id INTEGER");
            q.Append(@", ");
            q.Append(@"item_alias_id INTEGER");
            q.Append(@", ");
            q.Append(@"source_id INTEGER");
            q.Append(@", ");
            q.Append(@"date INTEGER");
            q.Append(@", ");
            q.Append(@"year INTEGER");
            q.Append(@", ");
            q.Append(@"month INTEGER");
            q.Append(@", ");
            q.Append(@"day INTEGER");
            q.Append(@", ");
            q.Append(@"time INTEGER");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(person_id) REFERENCES persons(person_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(task_id) REFERENCES tasks(task_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(task_alias_id) REFERENCES task_aliases(task_alias_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(subtask_id) REFERENCES subtasks(subtask_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(subtask_alias_id) REFERENCES subtask_aliases(subtask_alias_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(item_id) REFERENCES items(item_id)");
            q.Append(@", ");
            q.Append(@"FOREIGN KEY(item_alias_id) REFERENCES item_aliases(item_alias_id)");
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
                    LastQuery = q;
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
            catch (Exception ex)
            {
                trans.Rollback();
                LastErrorMessage = $"Query Failed: {ex.Message}\r\nLastQuery: {LastQuery}";
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

        public async Task<bool> LoadLogFile(string person, string rootPath, string filePath)
        {
            SqliteTransaction trans = conn.BeginTransaction();

            try
            {
                // ログファイルを開く
                var log = new LogReader {
                    RootPath = rootPath,
                    FilePath = filePath
                };
                if (!log.Open())
                {
                    return false;
                }
                // ログファイルの内容をDBに展開
                // person_id取得
                var personId = await QueryGetPersonId(trans, person);
                // ログファイルが登録済みかチェック
                var (logUpdate, sourceId) = await QueryCheckSource(personId, log);
                bool result;
                switch (logUpdate)
                {
                    case SourceCheck.Update:
                        if (sourceId != -1)
                        {
                            result = await LoadLogFileUpdate(trans, personId, sourceId, log);
                        }
                        break;

                    case SourceCheck.NewAdd:
                        // SourceInfo登録
                        sourceId = await QuerySetSourceInfos(trans, personId, log);
                        // ログ新規追加
                        result = await LoadLogFileAdd(trans, personId, sourceId, log);
                        break;

                    default:
                        // NoReqは何もしない
                        break;
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
            catch (Exception ex)
            {
                trans.Rollback();
                LastErrorMessage = $"Query Failed: {ex.Message}\r\nLastQuery: {LastQuery}";
                return false;
            }
        }

        private async Task<bool> LoadLogFileUpdate(SqliteTransaction trans, int personId, int sourceId, LogReader log)
        {
            try
            {
                bool result;
                //
                var row = await QueryUpdateSourceInfos(trans, sourceId, log);
                // 前回ログ削除
                var num = await QueryDeleteWorkTime(trans, sourceId);
                // 今回ログ登録
                result = await LoadLogFileAdd(trans, personId, sourceId, log);

                return result;
            }
            catch
            {
                throw;
            }
        }

        private async Task<bool> LoadLogFileAdd(SqliteTransaction trans, int personId, int sourceId, LogReader log)
        {
            // ログファイルの新規登録
            try
            {
                while (!log.EOF)
                {
                    // ログデータ取得
                    var item = log.Get();
                    // タスク登録
                    var taskId = await QueryCheckTasks(trans, item);
                    if (taskId == -1)
                    {
                        taskId = await QuerySetTasks(trans, item);
                    }
                    // タスクAlias登録
                    var aliasId = await QueryCheckTaskAliases(trans, item);
                    if (aliasId == -1)
                    {
                        aliasId = await QuerySetTaskAliases(trans, item);
                    }
                    // サブタスク登録
                    var subtaskId = await QueryCheckSubTasks(trans, item);
                    if (subtaskId == -1)
                    {
                        subtaskId = await QuerySetSubTasks(trans, item);
                    }
                    // サブタスクAlias登録
                    var subtaskAliasId = await QueryCheckSubTaskAliases(trans, item);
                    if (subtaskAliasId == -1)
                    {
                        subtaskAliasId = await QuerySetSubTaskAliases(trans, item);
                    }
                    // Item,WorkTimeは、WorkTimeがゼロでないときだけ登録する
                    if (item.Time == 0) continue;
                    // Item登録
                    var itemId = await QueryCheckItems(trans, item);
                    if (itemId == -1)
                    {
                        itemId = await QuerySetItems(trans, item);
                    }
                    // ItemAlias登録
                    var itemAliasId = await QueryCheckItemAliases(trans, item);
                    if (itemAliasId == -1)
                    {
                        itemAliasId = await QuerySetItemAliases(trans, item);
                    }
                    // work_time登録
                    var work_time_id = await QuerySetWorkTime(trans, item, log, personId, taskId, aliasId, subtaskId, subtaskAliasId, itemId, itemAliasId, sourceId);
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
                query.Append($@" WHERE person_name = '{person}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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
                query.Append($@"INSERT INTO persons (person_name) VALUES ('{person}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
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

        private async Task<(SourceCheck, int)> QueryCheckSource(int person_id, LogReader log)
        {
            try
            {
                // クエリ作成
                // 更新日時が同じか新しいログが登録済みなら何もしない
                // 更新日時が古いか登録が無いとき、
                var query = new StringBuilder();
                query.Append(@"SELECT source_id, source_lastmodify_date FROM source_infos");
                query.Append($@" WHERE person_id = {person_id} AND source_name = '{log.FileId}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var check = SourceCheck.NewAdd;
                        var id = -1;
                        // 結果読み出し
                        // 1件しかない前提
                        while (reader.Read() == true)
                        {
                            // 更新日時が同じか新しい場合は更新不要
                            // 更新日時が古い場合は更新する
                            // ここで1件もヒットしないならデータが無いので新規登録
                            var dbDate = log.Serial2DateTime((long)reader["source_lastmodify_date"]);
                            if (log.LastWriteTime.CompareTo(dbDate) > 0)
                            {
                                // LastWriteTimeの方が新しいので更新
                                check = SourceCheck.Update;
                                id = (int)(long)reader["source_id"];
                            }
                            else
                            {
                                // DB日時と同じか古いので何もしない
                                check = SourceCheck.NoReq;
                            }
                        }
                        return (check, id);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QueryUpdateSourceInfos(SqliteTransaction trans, int sourceId, LogReader log)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"UPDATE source_infos");
                query.Append($@" SET source_lastmodify_date = {log.LastWriteTime.ToBinary()}");
                query.Append($@" WHERE source_id = {sourceId}");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
                    return await command.ExecuteNonQueryAsync();
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
                query.Append($@"INSERT INTO source_infos (person_id, source_name, source_lastmodify_date)");
                query.Append($@" VALUES ('{person_id}', '{log.FileId}', '{log.LastWriteTime.ToBinary()}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がidになってるはず
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
                query.Append($@" WHERE task_code = '{item.Code}' AND task_name = '{item.Name}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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
                query.Append($@"INSERT INTO tasks (task_code, task_name)");
                query.Append($@" VALUES ('{item.Code}', '{item.Name}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がidになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QueryCheckTaskAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT task_alias_id FROM task_aliases");
                query.Append($@" WHERE task_alias_name = '{item.Alias}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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

        private async Task<int> QuerySetTaskAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO task_aliases (task_alias_name)");
                query.Append($@" VALUES ('{item.Alias}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
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

        private async Task<int> QueryCheckSubTasks(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT subtask_id FROM subtasks");
                query.Append($@" WHERE subtask_code = '{item.SubCode}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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

        private async Task<int> QuerySetSubTasks(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO subtasks (subtask_code)");
                query.Append($@" VALUES ('{item.SubCode}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
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

        private async Task<int> QueryCheckSubTaskAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT subtask_alias_id FROM subtask_aliases");
                query.Append($@" WHERE subtask_alias_name = '{item.SubAlias}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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

        private async Task<int> QuerySetSubTaskAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO subtask_aliases (subtask_alias_name)");
                query.Append($@" VALUES ('{item.SubAlias}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
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

        private async Task<int> QueryCheckItems(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT item_id FROM items");
                query.Append($@" WHERE item_name = '{item.Item}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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

        private async Task<int> QuerySetItems(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO items (item_name)");
                query.Append($@" VALUES ('{item.Item}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がidになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QueryCheckItemAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"SELECT item_alias_id FROM item_aliases");
                query.Append($@" WHERE item_alias_name = '{item.ItemAlias}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
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

        private async Task<int> QuerySetItemAliases(SqliteTransaction trans, LogType item)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO item_aliases (item_alias_name)");
                query.Append($@" VALUES ('{item.ItemAlias}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
                    command.ExecuteNonQuery();
                    // last_insert_rowid() がidになってるはず
                    return await GetLastInsertRowId(trans);
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<int> QuerySetWorkTime(SqliteTransaction trans, LogType item, LogReader log, int person_id, int task_id, int task_alias_id, int subtask_id, int subtask_alias_id, int item_id, int item_alias_id, int source_id)
        {
            try
            {
                // WorkTimeの日時はファイル名依存とする
                long date = log.FileDateTime.ToBinary();
                var year = log.FileDateTime.Year;
                var month = log.FileDateTime.Month;
                var day = log.FileDateTime.Day;
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"INSERT INTO work_times (person_id, task_id, task_alias_id, subtask_id, subtask_alias_id, item_id, item_alias_id, source_id, date, year, month, day, time)");
                query.Append($@" VALUES ('{person_id}', '{task_id}', '{task_alias_id}', '{subtask_id}', '{subtask_alias_id}', '{item_id}', '{item_alias_id}', '{source_id}', '{date}', '{year}', '{month}', '{day}', '{item.Time}')");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.Transaction = trans;
                    command.CommandText = LastQuery;
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

        private async Task<int> QueryDeleteWorkTime(SqliteTransaction trans, int sourceId)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append($@"DELETE FROM work_times");
                query.Append($@" WHERE source_id = '{sourceId}'");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                // 登録チェック
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
                    if (trans != null) command.Transaction = trans;
                    return await command.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                throw;
            }
        }


        public async Task<bool> QueryGetTaskCode<T>(ICollection<(T code,T name)> list)
        {
            try
            {
                // クエリ作成
                var query = new StringBuilder();
                query.Append(@"SELECT task_code, task_name FROM tasks");
                query.Append(@";");
                LastQuery = query.ToString();
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = LastQuery;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // 結果読み出し
                        while (reader.Read() == true)
                        {
                            var code = (T)reader["task_code"];
                            var name = (T)reader["task_name"];
                            list.Add((code,name));
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Query Failed: {ex.Message}";
                return false;
            }
        }

        public async Task<bool> QueryGetSelectResult(DataTable tbl, string query)
        {
            try
            {
                bool initCol = false;
                LastQuery = query;
                // クエリ実行
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = query;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // 結果読み出し
                        while (reader.Read() == true)
                        {
                            var count = reader.FieldCount;
                            // 列作成
                            if (!initCol)
                            {
                                initCol = true;
                                for (int i = 0; i < count; i++)
                                {
                                    tbl.Columns.Add(reader.GetName(i));
                                }
                            }
                            // 行作成
                            int idx = 0;
                            var row = tbl.NewRow();
                            foreach (DataColumn col in tbl.Columns)
                            {
                                row[col] = reader.GetValue(idx);
                                idx++;
                            }
                            tbl.Rows.Add(row);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Query Failed: {ex.Message}";
                return false;
            }
        }
    }

    class LogType
    {
        // MainTask
        public string Code { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        // SubTask
        public string SubCode { get; set; }
        public string SubAlias { get; set; }
        // Item
        public string Item { get; set; }
        public string ItemAlias { get; set; }
        // Time(minute/LSB)
        public int Time { get; set; }
    }

    class LogReader : IDisposable
    {
        public string RootPath { get; set; }
        public string FilePath { get; set; }
        private StreamReader reader;
        private string buff;
        public DateTime LastWriteTime { get; set; }
        public long LastWriteTimeSerial
        {
            get { return LastWriteTime.ToBinary(); }
        }
        public string FileId { get; set; }
        public DateTime FileDateTime { get; set; }

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
            if (File.Exists(FilePath))
            {
                GetFileDateTime();
                var fi = new FileInfo(FilePath);
                // ログフォルダからのパスを含めて、ログファイルのユニークなIDとする
                {
                    var from = new Uri(RootPath.Replace("%", "%25"));
                    var to = new Uri(FilePath.Replace("%", "%25"));
                    var rel = from.MakeRelativeUri(to);
                    FileId = rel.ToString();
                }
                LastWriteTime = fi.LastWriteTime;
                reader = new StreamReader(FilePath);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool GetFileDateTime()
        {
            var match = Util.RegexFileName.Match(FilePath);
            if (match.Success)
            {
                int year, month, day;
                try
                {
                    year = int.Parse(match.Groups[1].ToString());
                    month = int.Parse(match.Groups[2].ToString());
                    day = int.Parse(match.Groups[3].ToString());
                }
                catch
                {
                    year = 0;
                    month = 0;
                    day = 0;
                }
                FileDateTime = new DateTime(year, month, day);
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
                        min = int.Parse(match.Groups[8].ToString());
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
                        ItemAlias = match.Groups[7].ToString(),
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

        public DateTime Serial2DateTime(long serial)
        {
            return DateTime.FromBinary(serial);
        }
    }
}
