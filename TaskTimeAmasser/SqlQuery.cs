using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TaskTimeAmasser
{
    class QueryResultResource
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string TaskAlias { get; set; } = string.Empty;
        public string TaskAliasId { get; set; } = string.Empty;
        public string SubTaskCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Person { get; set; } = string.Empty;

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
        }
    }

}
