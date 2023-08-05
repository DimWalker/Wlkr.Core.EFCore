using System;
using System.Collections.Generic;
using System.Text;

namespace Wlkr.Core.EFCore.SqlServer
{
    /// <summary>
    /// 分数数据计算
    /// </summary>
    public class PagingUtil
    {
        private int _PageSize = 0;
        public int PageSize
        {
            get { return _PageSize != 0 ? _PageSize : TotalRecord; }
             set { _PageSize = value; }
        }
        public int PageIdx { get;  set; }
        public int TotalRecord { get; private set; }
        public int TotalPage { get; private set; }
        public int Skip { get; private set; }

        public PagingUtil(int pageSize, int pageIdx)
        {
            PageSize = pageSize;
            PageIdx = pageIdx;
        }

        public PagingUtil(int pageIdx)
        {
            PageSize = 20;
            PageIdx = pageIdx;
        }

        public int CalcPageParams(int totalRecord)
        {
            TotalRecord = totalRecord;
            if (PageSize > 0)
                TotalPage = Convert.ToInt32(Math.Ceiling(TotalRecord * 1.0 / PageSize));
            else
                TotalPage = 1;
            if (PageIdx < 1)
                PageIdx = 1;
            if (PageIdx > TotalPage && TotalPage > 0)
                PageIdx = TotalPage;
            Skip = PageSize * (PageIdx - 1);
            return Skip;
        }
    }
}
