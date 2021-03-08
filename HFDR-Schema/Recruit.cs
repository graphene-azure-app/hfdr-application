using System;
using System.Collections.Generic;

namespace HFDR_Schema
{
    public class Recruit
    {
        public string id { get; set; }
        public DateTime APPLIED_DATE { get; set; }
        public string LAST_NAME { get; set; }
        public string FIRST_NAME { get; set; }
        public string MIDDLE_NAME { get; set; }
        public string ZIP { get; set; }
        public int AGE { get; set; }
        public string EMAIL { get; set; }

    }
    public class PagedRecruit
    {
        public bool HasMoreResults { get; set; }
        public string PagingToken { get; set; }
        public List<Recruit> Results { get; set; }

    }
    public class RecruitCnt
    {
        public int TOTAL { get; set; }
    }
}
