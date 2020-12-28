using System.Windows.Forms;

namespace FileSearch
{
    // 업데이트시 깜빡꺼림을 제거한 리스트뷰로 사용
    class ListViewNF : ListView
    {
        public ListViewNF()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }
    }
}
