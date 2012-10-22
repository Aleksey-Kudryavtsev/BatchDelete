using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Web;
using Microsoft.SharePoint;

namespace BatchDelete
{
    class Program
    {
        private const int RootWebUrlArgumentIndex = 2;
        private const int SiteUrlArgumentIndex = 1;
        private const int BatchSizeArgumentIndex = 0;

        private static uint BatchSize;

        static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                PrintUsage();
                return;
            }

            if(!ReadBatchSizeFromCommandLine(args))
            {
                PrintUsage();
                Console.WriteLine("Batch size should be a positive integer");
                return;
            }

            using (SPSite site = new SPSite(args[SiteUrlArgumentIndex]))
            {
                if (args[RootWebUrlArgumentIndex] == "RecycleBin")
                {
                    CleanRecycleBin(site);
                }
                else
                {
                    CleanSites(args, site);
                }
            }
        }

        private static void CleanRecycleBin(SPSite site)
        {
            Console.WriteLine(string.Format("Site recycle bin contains {0} items.", site.RecycleBin.Count));

            while (site.RecycleBin.Count > 0)
            {
                SPRecycleBinItemCollection recycleBin = site.RecycleBin;

                Console.WriteLine(string.Format("Starting a new batch. Current item count: {0}. Batch size: {1}", recycleBin.Count, BatchSize));

                List<Guid> recycleBinItemsBatch = new List<Guid>();
                for (int i = 0; i < BatchSize && i < recycleBin.Count; i++)
                {
                    recycleBinItemsBatch.Add(recycleBin[i].ID);
                }

                recycleBin.Delete(recycleBinItemsBatch.ToArray());
                Console.WriteLine("Batch completed");

            }
        }

        private static void CleanSites(string[] args, SPSite site)
        {
            using (SPWeb web = GetSPWeb(args, site))
            {
                for (int i = RootWebUrlArgumentIndex + 1; i < args.Length; i++)
                {
                    string listUrl = args[i];
                    Console.WriteLine(string.Format("Cleaning list: {0}", listUrl));
                    SPList list = web.GetList(listUrl);
                    CleanList(list);
                }
            }
        }


        private static SPWeb GetSPWeb(string[] args, SPSite site)
        {
            string rootWebUrl = args[RootWebUrlArgumentIndex];
            SPWeb web;

            if (rootWebUrl == "root")
            {
                return site.RootWeb;
            }
            else
            {
                return site.OpenWeb(rootWebUrl);
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
            Console.WriteLine("usage: BatchDelete <batch_size> <site url> (RecycleBin | <subwebUrl> <list1 url> ... <listN url>)");
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

                Console.WriteLine(string.Format("Processed batch #{0} of {1} items in {2} second(s)", batchNumber, BatchSize, stopwatch.ElapsedMilliseconds / 1000.0));
                batchNumber++;
            }
        }

        private static SPQuery CreateGetAllItemsQuery()
        {
            SPQuery spQuery = new SPQuery();
            spQuery.Query = "";
            spQuery.ViewFields = "";
            spQuery.RowLimit = BatchSize;
            return spQuery;
        }

        private static void RunDeleteBatch(SPList list, string batchDeleteXmlCommand)
        {
            bool unsafeUpdate = list.ParentWeb.AllowUnsafeUpdates;
            try
            {
                list.ParentWeb.AllowUnsafeUpdates = true;

                Console.WriteLine(batchDeleteXmlCommand);

                Console.ReadKey(true);

                string result = list.ParentWeb.ProcessBatchData(batchDeleteXmlCommand);

                if(result != null)
                {
                    Console.WriteLine(result);
                }

                Console.ReadKey(true);
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


            StringBuilder commandFormatBuilder = new StringBuilder();

            commandFormatBuilder.Append("<Method>");
            commandFormatBuilder.Append("<SetList Scope=\"Request\">" + list.ID + "</SetList>");
            commandFormatBuilder.Append("<SetVar Name=\"ID\">{0}</SetVar>");
            commandFormatBuilder.Append("<SetVar Name=\"owsfileref\">{1}</SetVar>");
            commandFormatBuilder.Append("<SetVar Name=\"Cmd\">Delete</SetVar>");
            commandFormatBuilder.Append("</Method>");

            string commandFormat = commandFormatBuilder.ToString();

            foreach (SPListItem item in items)
            {
                xmlCommandStringBuilder.Append(string.Format(commandFormat, item.ID.ToString(), HttpUtility.UrlDecode((string)item[SPBuiltInFieldId.EncodedAbsUrl])));
            }


            xmlCommandStringBuilder.Append("</Batch>");
            return xmlCommandStringBuilder.ToString();
        }
    }
}
