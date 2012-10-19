using System;
using System.Diagnostics;
using System.Text;
using Microsoft.SharePoint;

namespace BatchDelete
{
    class Program
    {
        private const int SiteUrlArgumentIndex = 1;
        private const int BatchSizeArgumentIndex = 0;

        private static uint BatchSize;

        static void Main(string[] args)
        {
            if(args.Length < 3)
            {
                PrintUsage();
                return;
            }

            if(!ReadBatchSizeFromCommandLine(args))
            {
                PrintUsage();
                Console.WriteLine("Batch size should be an positive integer");
                return;
            }

            using(SPSite site = new SPSite(args[SiteUrlArgumentIndex]))
            {
                for (int i = SiteUrlArgumentIndex + 1; i < args.Length; i++)
                {
                    string listUrl = args[i];
                    Console.WriteLine(string.Format("Cleaning list: {0}", listUrl));
                    SPList list = site.RootWeb.GetList(listUrl);

                    CleanList(list);
                }
            }
        }

        private static bool ReadBatchSizeFromCommandLine(string[] args)
        {
            uint batchSize;
            if (uint.TryParse(args[BatchSizeArgumentIndex], out batchSize))
            {
                if(batchSize == 0)
                {
                    return false;
                }
                BatchSize = batchSize;
                return true;
            }
            
            return false;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("usage: BatchDelete <batch_size> <site url> <list1 url> ... <listN url>");
        }

        private static void CleanList(SPList list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            SPQuery spQuery = CreateGetAllItemsQuery();

            int batchNumber = 1;

            while (true)
            {
                // get ids of items to be deleted
                SPListItemCollection items = list.GetItems(spQuery);
                if (items.Count <= 0)
                    break;

                string batchDeleteXmlCommand = GetBatchDeleteXmlCommand(list, items);


                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                RunDeleteBatch(list, batchDeleteXmlCommand);

                stopwatch.Stop();

                Console.WriteLine(string.Format("Processed batch #{0} of {1} items in {2} second(s)", batchNumber, BatchSize, (stopwatch.Elapsed.Milliseconds/ 1000.0)));
                batchNumber++;
            }
        }

        private static SPQuery CreateGetAllItemsQuery()
        {
            SPQuery spQuery = new SPQuery();
            spQuery.Query = "";
            spQuery.ViewFields = "";
            spQuery.ViewAttributes = "Scope=\"RecursiveAll\"";
            spQuery.RowLimit = BatchSize;
            return spQuery;
        }

        private static void RunDeleteBatch(SPList list, string batchDeleteXmlCommand)
        {
            bool unsafeUpdate = list.ParentWeb.AllowUnsafeUpdates;
            try
            {
                list.ParentWeb.AllowUnsafeUpdates = true;
                list.ParentWeb.ProcessBatchData(batchDeleteXmlCommand);
            }
            finally
            {
                list.ParentWeb.AllowUnsafeUpdates = unsafeUpdate;
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
