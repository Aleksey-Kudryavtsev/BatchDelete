using System;
using System.Text;
using Microsoft.SharePoint;

namespace BatchDelete
{
    class Program
    {
        private const int BatchSize = 500;

        static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("usage: BatchDelete <site_url> <list1_name> ... <listN_name>");
            }

            using(SPSite site = new SPSite(args[0]))
            {
                for(int i=1; i<args.Length; i++)
                {
                    Console.WriteLine(string.Format("Cleaning list: {0}", args[i]));
                    SPList list = site.RootWeb.GetList(args[i]);

                    CleanList(list);
                }
            }
        }

        private static void CleanList(SPList list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            SPQuery spQuery = new SPQuery();
            spQuery.Query = "";
            spQuery.ViewFields = "";
            spQuery.ViewAttributes = "Scope=\"RecursiveAll\"";
            spQuery.RowLimit = BatchSize;

            int batchNumber = 1;

            while (true)
            {
                // get ids of items to be deleted
                SPListItemCollection items = list.GetItems(spQuery);
                if (items.Count <= 0)
                    break;

                string batchDeleteXmlCommand = GetBatchDeleteXmlCommand(list, items);

                bool unsafeUpdate = list.ParentWeb.AllowUnsafeUpdates;
                try
                {
                    list.ParentWeb.AllowUnsafeUpdates = true;
                    list.ParentWeb.ProcessBatchData(batchDeleteXmlCommand);
                    Console.WriteLine("Processed batch " + batchNumber);
                    batchNumber++;
                }
                finally
                {
                    list.ParentWeb.AllowUnsafeUpdates = unsafeUpdate;
                }
            }
        }

        private static string GetBatchDeleteXmlCommand(SPList list, SPListItemCollection items)
        {
            StringBuilder xmlCommandStringBuilder = new StringBuilder();
            xmlCommandStringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Batch>");
            string command = "<Method><SetList Scope=\"Request\">" + list.ID +
                             "</SetList><SetVar Name=\"ID\">{0}</SetVar><SetVar Name=\"Cmd\">Delete</SetVar></Method>";

            foreach (SPListItem item in items)
            {
                xmlCommandStringBuilder.Append(string.Format(command, item.ID.ToString()));
            }
            xmlCommandStringBuilder.Append("</Batch>");
            return xmlCommandStringBuilder.ToString();
        }
    }
}
