using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTimeAmasser
{
    public static class SqlQuery
    {

        static public string MakeQuerySelectTaskList(QueryResultResource resource, QueryFilterTask filter)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT DISTINCT");
            query.AppendLine($@"  task_code AS '{resource.TaskCode}',");
            query.AppendLine($@"  task_name AS '{resource.TaskName}',");
            query.AppendLine($@"  task_alias_name AS '{resource.TaskAlias}',");
            query.AppendLine($@"  task_alias_id AS '{resource.TaskAliasId}'");
            query.AppendLine(@"FROM work_times");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            // WHERE: 条件設定
            if (filter.EnableTasks || filter.EnablePersonId)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"  {and}person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            query.AppendLine(@"  task_code, task_name, task_alias_name");
            query.Append(@";");
            return query.ToString();
        }

        static public string MakeQuerySelectPersonList(QueryResultResource resource, QueryFilterTask filter)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT DISTINCT");
            query.AppendLine($@"  person_id AS '{resource.PersonId}',");
            query.AppendLine($@"  person_name AS '{resource.Person}'");
            query.AppendLine(@"FROM persons");
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            query.AppendLine(@"  person_id");
            query.Append(@";");
            return query.ToString();
        }

        static public string MakeQuerySelectDateRange(QueryResultResource resource, QueryFilterTask filter)
        {
            // クエリ作成
            if (!(filter.EnableTasks || filter.EnablePersonId))
            {
                return "SELECT max(w.date) AS MAX, min(w.date) AS MIN FROM work_times w;";
            }
            else
            {
                return MakeQuerySelectDateRangeFilter(filter);
            }
        }
        static public string MakeQuerySelectDateRangeFilter(QueryFilterTask filter)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT max(time_tbl.date) AS MAX, min(time_tbl.date) AS MIN");
            query.AppendLine(@"FROM");
            query.AppendLine(@"  (SELECT w.date");
            query.AppendLine(@"   FROM");
            query.AppendLine(@"     work_times w");
            // タスクフィルターサブテーブル
            if (filter.EnableTaskCode || filter.EnableTaskName)
            {
                var and = "";
                // タスク名とタスクIDの両方をフィルターするか？
                query.AppendLine(@"     ,");
                query.AppendLine($@"     (SELECT t.task_id AS task_id FROM tasks t");
                query.AppendLine($@"      WHERE");
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"        {and}t.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"        {and}t.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                query.AppendLine($@"     ) AS filter_task");
            }
            // タスクエイリアスフィルターサブテーブル
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"     ,");
                query.AppendLine($@"     (SELECT a.task_alias_id AS alias_id FROM task_aliases a");
                query.AppendLine($@"      WHERE");
                //
                var and = "";
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"        {and}a.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"        {and}a.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                query.AppendLine($@"     ) AS filter_alias");
            }
            if (filter.EnableTaskCode || filter.EnableTaskName || filter.EnableTaskAlias || filter.EnablePersonId)
            {
                var and = "";
                query.AppendLine(@"   WHERE");
                if (filter.EnableTaskCode || filter.EnableTaskName)
                {
                    query.AppendLine($@"     {and}w.task_id = filter_task.task_id");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"     {and}w.task_alias_id = filter_alias.alias_id");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"     {and}w.person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            query.AppendLine(@"  ) AS time_tbl");
            query.Append(@";");
            return query.ToString();
        }


        static public string MakeQuerySelectSubTotal(QueryResultResource resource, QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{resource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{resource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{resource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.subtask_code AS '{resource.SubTaskCode}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   subtasks");
            if (filter.EnableTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            if (filter.EnablePersonId)
            {
                query.AppendLine(@"   LEFT OUTER JOIN persons");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"  {and}time_tbl.person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_code");
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_id");
            query.Append(@";");
            return query.ToString();
        }

        static public void MakeQuerySelectTermColumns(StringBuilder query, QueryFilterTerm term)
        {
            // カラム作成
            // 指定期間
            for (int i = 0; i < term.Terms.Count; i++)
            {
                var thre = term.Terms[i];
                query.AppendLine($@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 WHEN {thre.boundLo} <= time_tbl.date AND time_tbl.date < {thre.boundHi} THEN time_tbl.time ELSE 0 END) AS '工数({thre.date})',");
            }
            // 期間全体
            var boundLo = term.Terms.First().boundLo;
            var boundHi = term.Terms.Last().boundHi;
            query.AppendLine($@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 WHEN {boundLo} <= time_tbl.date AND time_tbl.date < {boundHi} THEN time_tbl.time ELSE 0 END) AS '工数(合計)'");
        }



        static public string MakeQuerySelectItemTotal(QueryResultResource resource, QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{resource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{resource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{resource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.subtask_code AS '{resource.SubTaskCode}',");
            query.AppendLine($@"  time_tbl.item_name AS '{resource.ItemName}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   SELECT task_id, task_alias_id, subtask_id, item_id, date, time, person_id FROM work_times");
            query.AppendLine(@"     UNION ALL");
            query.AppendLine(@"   SELECT DISTINCT task_id, task_alias_id, s.subtask_id, s.item_id, 0, 0, person_id");
            query.AppendLine(@"   FROM");
            query.AppendLine(@"     subtask_item_rel s");
            query.AppendLine(@"     LEFT OUTER JOIN ( SELECT subtask_id, subtask_alias_id, item_id, item_alias_id, task_id, task_alias_id, person_id FROM work_times ) AS w");
            query.AppendLine(@"  ) AS item_tbl");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN items NATURAL LEFT OUTER JOIN subtasks");
            if (filter.EnableTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  ) AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"  {and}time_tbl.person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_code,");
            query.AppendLine(@"  time_tbl.item_name");
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_id,");
            query.AppendLine(@"  time_tbl.item_id");
            query.Append(@";");
            return query.ToString();
        }



        static public string MakeQuerySelectPersonInfoTerm(QueryResultResource resource, QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            query.AppendLine($@"  person_name AS '{resource.Person}',");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  task_code AS '{resource.TaskCode}',");
                query.AppendLine($@"  task_name AS '{resource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  task_alias_name AS '{resource.TaskAlias}',");
            }
            // ログ数情報
            // 基本的に期間指定ありき
            if (!term.IsActive)
            {
                // throw;
            }
            for (int i = 0; i < term.Terms.Count; i++)
            {
                var thre = term.Terms[i];
                var comma = i == term.Terms.Count - 1 ? string.Empty : ",";
                query.AppendLine($@"  Sum(CASE WHEN {thre.boundLo} <= date AND date < {thre.boundHi} THEN 1 ELSE 0 END) AS 'ログ数({thre.date})'{comma}");
            }
            query.AppendLine(@"FROM");
            query.AppendLine(@"  work_times");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN persons");
            if (filter.EnableSubTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN subtasks");
            }
            if (filter.EnableTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"  {and}person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  task_code,");
            }
            query.AppendLine(@"  person_id");
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            /*
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            */
            query.AppendLine(@"  person_id");
            query.Append(@";");
            return query.ToString();
        }



        static public string MakeQuerySelectPersonTotal(QueryResultResource resource, QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{resource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{resource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{resource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.person_name AS '{resource.Person}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   persons");
            if (filter.EnableSubTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN subtasks");
            }
            if (filter.EnableTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
                if (filter.EnablePersonId)
                {
                    query.AppendLine($@"  {and}time_tbl.person_id IN ({filter.PersonId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.person_name");
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.person_id");
            query.Append(@";");
            return query.ToString();
        }
    }
}
