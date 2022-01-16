using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TaskTimeAmasser
{
    public class QueryResultResource
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string TaskAlias { get; set; } = string.Empty;
        public string TaskAliasId { get; set; } = string.Empty;
        public string SubTaskCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Person { get; set; } = string.Empty;
        public string PersonId { get; set; } = string.Empty;

        public QueryResultResource()
        {
            // ResourceDictionary取得
            ResourceDictionary dic = new ResourceDictionary
            {
                Source = new Uri("/TaskTimeAmasser;component/Resources/GUIDictionary.xaml", UriKind.Relative)
            };
            // 情報展開
            TaskCode = dic["GuiDispQueryResultTaskCode"].ToString();
            TaskName = dic["GuiDispQueryResultTaskName"].ToString();
            TaskAlias = dic["GuiDispQueryResultTaskAlias"].ToString();
            TaskAliasId = dic["GuiDispQueryResultTaskAliasId"].ToString();
            SubTaskCode = dic["GuiDispQueryResultSubTaskCode"].ToString();
            ItemName = dic["GuiDispQueryResultItemName"].ToString();
            Person = dic["GuiDispQueryResultPerson"].ToString();
            PersonId = dic["GuiDispQueryResultPersonId"].ToString();
        }
    }

    public class QueryFilterTask
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string TaskAlias { get; set; } = string.Empty;
        public string TaskAliasId { get; set; } = string.Empty;
        public string ExcludeTaskCode { get; set; } = string.Empty;
        // SubTaskFilter
        public string SubTaskCode { get; set; } = string.Empty;
        public List<string> ExcludeSubTaskCode { get; set; } = new List<string>();
        // PersonFilter
        public string PersonId { get; set; } = string.Empty;

        // TaskFilter
        public bool IsActive { get; set; } = false;
        public bool EnableTaskCode { get; set; } = false;
        public bool EnableTaskName { get; set; } = false;
        public bool EnableTaskAlias { get; set; } = false;
        public bool EnableTaskAliasId { get; set; } = false;
        public bool EnableExcludeTaskCode { get; set; } = false;
        public bool EnableTasks { get; set; } = false;
        // SubTaskFilter
        public bool EnableSubTaskCode { get; set; } = false;
        public bool EnableExcludeSubTaskCode { get; set; } = false;
        public bool EnableSubTasks { get; set; } = false;
        // PersonFilter
        public bool EnablePersonId { get; set; } = false;

        public QueryFilterTask() { }

        public void Init()
        {
            // TaskFilter
            EnableTaskCode = TaskCode != "<指定なし>";
            EnableTaskName = TaskName.Length > 0;
            EnableTaskAlias = TaskAlias.Length > 0;
            EnableTaskAliasId = TaskAliasId.Length > 0;
            EnableExcludeTaskCode = ExcludeTaskCode.Length > 0;
            EnableTasks = (EnableTaskCode || EnableTaskName || EnableTaskAlias || EnableTaskAliasId || EnableExcludeTaskCode);
            // SubTaskFilter
            EnableSubTaskCode = SubTaskCode.Length > 0;
            EnableExcludeSubTaskCode = ExcludeSubTaskCode.Count > 0;
            EnableSubTasks = (EnableSubTaskCode || EnableExcludeSubTaskCode);
            // PersonFilter
            EnablePersonId = PersonId.Length > 0;
            //
            IsActive = (EnableTaskCode || EnableTaskName || EnableTaskAlias || EnableTaskAliasId || EnableExcludeTaskCode || EnableSubTaskCode || EnableExcludeSubTaskCode || EnablePersonId);
        }

    }

    public class QueryFilterTerm
    {
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public int Unit { get; set; }

        public List<(long boundLo, long boundHi, string date)> Terms { get; set; } = new List<(long boundLo, long boundHi, string date)>();

        public QueryFilterTerm()
        {
            IsActive = false;
        }

        public bool IsActive { get; set; } = false;

        public void Init()
        {
            // 前後関係チェック
            if (Begin > End)
            {
                var temp = Begin;
                Begin = End;
                End = temp;
            }
            MakeTerm();
            // 有効化
            IsActive = true;
        }

        private void MakeTerm()
        {
            switch (Unit)
            {
                case 0:
                    // 日単位
                    MakeTermDay();
                    break;
                case 1:
                    // 月単位
                    MakeTermMonth();
                    break;
                case 2:
                    // 年単位
                    MakeTermYear();
                    break;
                default:
                    Terms.Clear();
                    break;
            }
        }

        private void MakeTermDay()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = Begin.Date;
            // Endの日付まで含めるものとする
            // 日時の差分をバッファサイズとする
            var span = End.Date - boundCurr;
            Terms.Clear();
            Terms.Capacity = span.Days + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddDays(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy/MM/dd")));
                boundCurr = boundNext;
            }
        }

        private void MakeTermMonth()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = new DateTime(Begin.Year, Begin.Month, 1);
            // Endの日付まで含めるものとする
            var boundEnd = new DateTime(End.Year, End.Month, 1);
            // 日時の差分をバッファサイズとする
            var span = (boundEnd.Month - boundCurr.Month) + (12 * (boundEnd.Year - boundCurr.Year));
            Terms.Clear();
            Terms.Capacity = span + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddMonths(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy/MM")));
                boundCurr = boundNext;
            }
        }

        private void MakeTermYear()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = new DateTime(Begin.Year, 1, 1);
            // Endの日付まで含めるものとする
            var boundEnd = new DateTime(End.Year, 1, 1);
            // 日時の差分をバッファサイズとする
            var span = boundEnd.Year - boundCurr.Year;
            Terms.Clear();
            Terms.Capacity = span + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddYears(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy")));
                boundCurr = boundNext;
            }
        }
    }
}
