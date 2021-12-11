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
            if (filter.EnableTasks)
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
            }
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            query.AppendLine(@"  task_code, task_name, task_alias_name");
            query.Append(@";");
            return query.ToString();
        }

        static public string MakeQuerySelectDateRange(QueryResultResource resource, QueryFilterTask filter)
        {
            // クエリ作成
            if (!filter.EnableTasks)
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
            if (filter.EnableTaskCode || filter.EnableTaskName || filter.EnableTaskAlias)
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
            }
            query.AppendLine(@"  ) AS time_tbl");
            query.Append(@";");
            return query.ToString();
        }
    }
}
