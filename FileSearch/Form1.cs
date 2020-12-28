using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace FileSearch
{
    public partial class Form1 : Form
    {
        Thread t1 = null; // 쓰레드 생성
        bool searchStop = false;  // 검색 시작/중지 (true : 중지)
        ManualResetEvent locker = new ManualResetEvent(true); // 쓰레드의 일시정지 재시작을 위해 사용

        public Form1()
        {
            InitializeComponent();

            backgroundWorker1.WorkerReportsProgress = true; // 작업 진행률을 보고할 수 있게 설정
            backgroundWorker1.WorkerSupportsCancellation = true; // 작업을 취소할 수 있도록 설정한다.
            CheckForIllegalCrossThreadCalls = false; // 백그라운드 작업 중 크로스 스레드 관련 문제 생략
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            // 리스트뷰 초기 설정
            listView1.Columns.Add("파일명", 200);
            listView1.Columns.Add("파일경로", 260);
            listView1.Columns.Add("행번호", 50, HorizontalAlignment.Right);
            listView1.Columns.Add("내용", 320);

            listView1.View = View.Details;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.ShowItemToolTips = true;

            label4.Text = "";
            label6.Text = "0 %";
        }

        // ---------------------------------------------------------------------------------------------------------- //
        private void btnLocation_Click(object sender, EventArgs e)
        {
            // 폴더 다이얼로그 띄우기
            FolderBrowserDialog folderPicker = new FolderBrowserDialog();
            if (folderPicker.ShowDialog() == DialogResult.OK) // 확인 눌렀을 때
            {
                txtLocation.Text = folderPicker.SelectedPath;
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        private void btnSearch_Click(object sender, EventArgs e)
        {
            // 경로지정 확인
            if (txtLocation.Text.Trim() == "")
            {
                label4.Text = "경로를 지정해주세요...";
                searchStart();
                MessageBox.Show("경로를 지정해주세요.");
                return;
            }

            btnStop.Text = "일시 정지";
            btnSearch.Enabled = false; // 검색 버튼 비활성화

            if (!backgroundWorker1.IsBusy) // 백그라운드가 실행 중이 아닐 때 검색 시작
            {
                listView1.Items.Clear();
                label4.Text = "잠시만 기다려주세요...";
                label6.Text = "계산중..";
                progressBar1.Style = ProgressBarStyle.Marquee;
                backgroundWorker1.RunWorkerAsync(); // 백그라운드 작업 실행
            }
            else // 일시 정지 후 다시 이어서 검색
            {
                locker.Set(); // 일시 정지 했을 때 다시 시작 : ManualResetEvent - true로 반환
                btnSearch.Text = "검색중...";
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 검색 중지 버튼 클릭
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (btnStop.Text == "검색 중지")
            {
                searchStop = true;
                locker.Set();
                if (label6.Text == "0 %")
                    label4.Text = "검색 중지.";
                searchStart();
            }
            else if (btnStop.Text == "일시 정지")
            {
                locker.Reset(); // WaitOne 부분에서 정지 : ManualResetEvent - false로 반환
                btnStop.Text = "검색 중지";
                btnSearch.Enabled = true;
                btnSearch.Text = "계속 검색";
                label4.Text = "일시 정지";
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 하위 디렉토리 검색 관련, 엑세스 처리
        static IEnumerable<string> GetFiles(string path, string filename)
        {
            // Queue 하나 생성
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path); // 큐에 경로 저장
            while (queue.Count > 0) // 경로의 개수만큼 반복
            {
                path = queue.Dequeue(); // 큐에 있는 경로중 제일 먼저 들어온 것을 꺼내서 path에 저장
                try
                {
                    // 바로 아래 경로들 큐에 넣기
                    foreach (string subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                string[] files = null;
                try
                {
                    // 파일명 비우고 검색 시
                    if (filename == "")
                        files = Directory.GetFiles(path);
                    // 파일명 입력 후 검색 시
                    else
                        files = Directory.GetFiles(path, filename);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                // 파일이 존재하면
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        // files 개수만큼 IEnumerable<string> 전달
                        yield return files[i];
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // https://docs.microsoft.com/ko-kr/previous-versions/visualstudio/visual-studio-2010/ms171728(v=vs.100)/ 참고
        // http://happyguy81.tistory.com/59 참고
        // 
        // 쓰레드를 실행 중 컨트롤에 접근하기 위해서 사용
        delegate void ProgressBarMaximum(int i);
        private void SetMax(int i)
        {
            if (this.InvokeRequired)
            {
                ProgressBarMaximum d = new ProgressBarMaximum(SetMax);
                try
                {
                    this.Invoke(d, new object[] { i });
                }
                catch
                {
                    return;
                }
            }
            else
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 시작버튼 되돌리기
        private void searchStart()
        {
            btnSearch.Enabled = true;
            btnSearch.Text = "검색 시작";
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 리스트 뷰 복사 처리
        private void listView1_KeyUp(object sender, KeyEventArgs e)
        {
            // 리스트 뷰 복사 처리
            if (e.Control && e.KeyCode == Keys.C) // Ctrl+C 눌렀을 때
            {
                int selectIndex = listView1.FocusedItem.Index; // 선택한 행의 인덱스를 가져옴

                try // 클립보드에 저장
                {
                    Clipboard.SetText("파일명\t: " + listView1.Items[selectIndex].SubItems[0].Text +
                        "\r\n파일경로: " + listView1.Items[selectIndex].SubItems[1].Text +
                        "\r\n행번호\t: " + listView1.Items[selectIndex].SubItems[2].Text +
                        "\r\n내용\t: " + listView1.Items[selectIndex].SubItems[3].Text);
                }
                catch // 내용이 없을 때
                {
                    Clipboard.SetText("파일명\t: " + listView1.Items[selectIndex].SubItems[0].Text +
                        "\r\n파일경로: " + listView1.Items[selectIndex].SubItems[1].Text +
                        "\r\n행번호\t: " + "" +
                        "\r\n내용\t: " + "");
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 파일명 정렬 (자세히 x, 나중에 확인)
        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column > 0) // 클릭한 칼럼의 번호가 0보다 클 때 (파일명만 정렬 하기 위해서)
            {
                return;
            }

            listView1.Columns[e.Column].Text = listView1.Columns[e.Column].Text.Replace(" ▼", "");
            listView1.Columns[e.Column].Text = listView1.Columns[e.Column].Text.Replace(" ▲", "");

            if (listView1.Sorting == SortOrder.Ascending || listView1.Sorting == SortOrder.None)
            {
                listView1.Sorting = SortOrder.Descending;
                listView1.Columns[e.Column].Text = listView1.Columns[e.Column].Text + " ▼";
            }
            else
            {
                listView1.Sorting = SortOrder.Ascending;
                listView1.Columns[e.Column].Text = listView1.Columns[e.Column].Text + " ▲";
            }

            listView1.Sort();
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 폼 닫을 때 처리되는 이벤트 
        // 검색 도중 창을 닫을 때 반복문이 계속 돌고 있는 경우를 막아주기
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            searchStop = true;
            backgroundWorker1.CancelAsync();
            if (t1 == null)
                this.Dispose();
            else if (t1.ThreadState != System.Threading.ThreadState.Aborted)
                t1.Abort();
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 리스트뷰 더블클릭/엔터 시 해당 파일이 메모장으로 열림(해당 파일에 맞게 열림)
        private void listView1_ItemActivate(object sender, EventArgs e)
        {
            //Process.Start("notepad.exe", listView1.Items[listView1.FocusedItem.Index].SubItems[1].Text);
            Process.Start(listView1.Items[listView1.FocusedItem.Index].SubItems[1].Text);
        }
        // ---------------------------------------------------------------------------------------------------------- //
        // 텍스트박스에서 엔터로 검색 되게 처리 (아래 세개)
        private void txtFileNm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSearch_Click(sender, e);
            }
        }

        private void txtContent_KeyUp(object sender, KeyEventArgs e)
        {
            txtFileNm_KeyUp(sender, e);
        }

        private void txtExtension_KeyUp(object sender, KeyEventArgs e)
        {
            txtFileNm_KeyUp(sender, e);
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 백그라운드 작업 진행
        DateTime startTime;  // 검색 시작 시간 설정
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            startTime = DateTime.Now; // 검색 시작 시간
            bool extensionBool = false; // 확장자 체크
            searchStop = false; // 검색 중지 체크
            
            try
            {
                List<string> fileExtension = new List<String>(); // 파일 확장자 저장할 곳
                IEnumerable<string> files = null; // 파일들 저장할 곳

                // ---- 파일명 검색 처리 ---- //
                if (txtFileNm.Text.Trim() == "") // 파일명이 빈 칸일 경우
                {
                    files = GetFiles(txtLocation.Text, "");
                }
                else // 파일명 넣고 검색할 경우
                {
                    files = GetFiles(txtLocation.Text.Trim(), "*" + txtFileNm.Text.Trim() + "*");
                }

                // ---- 수량자 문제 처리 ---- //
                //   문자 그대로 받아오도록   //
                string replaceText1 = txtContent.Text.Trim().Replace("?", "\\?");
                string replaceText2 = replaceText1.Replace("*", "\\*");
                string replaceText3 = replaceText2.Replace("+", "\\+");
                string replaceText4 = replaceText3.Replace(".", "\\.");
                string content = replaceText4;

                // ---- 확장자 검색 처리 ---- //
                if (txtExtension.Text.Trim() != "") // 확장자 TextBox가 빈 칸이 아닐 경우
                {
                    extensionBool = true; // 확장자 있음을 알려주는 bool 처리
                    string[] ext = txtExtension.Text.Split(';'); // ';' 문자로 구분
                    for (int i = 0; i < ext.Length; i++)
                    {
                        fileExtension.Add(ext[i].ToLower().Trim()); // 확장자를 fileExtension 배열에 저장
                    }
                }

                // ---- 내용 검색 처리 ---- //
                if (content != "") // 내용을 입력 했을 경우
                {
                    // ---- progressBar Maximum 값 구하기위한 파일 총 개수 ---- //
                    int total = 0; // 파일 100%일 때 개수
                    int cnt = 0; // 검색하고 있는 파일 개수
                    int pct = 0; // 계산된 퍼센트 
                    bool percentCheck = false; // 쓰레드 계산 끝나는 것을 확인하기 위한 bool 값

                    t1 = new Thread(new ThreadStart(delegate () // 쓰레드 사용
                    {
                        foreach (string file in files)
                        {
                            if (searchStop) // 검색 중지하면 멈춤
                                break;
                            // ---- 내용 검색 o, 확장자 검색 x ---- //
                            if (!extensionBool)
                            {
                                IEnumerable<string> read = File.ReadLines(file, Encoding.Default); // file을 읽고 read에 한줄씩 저장
                                foreach (string line in read) // 한 줄마다 반복
                                {
                                    // 대소문자 구분없이 line에 txtContnt.Text가 포함되어 있으면
                                    if (Regex.IsMatch(line, content, RegexOptions.IgnoreCase))
                                    {
                                        total++;
                                    }
                                }
                            }
                            // ---- 내용 검색 o, 확장자 검색 o ---- //
                            else
                            {
                                for (int i = 0; i < fileExtension.Count; i++) // 확장자 개수만큼 반복
                                {
                                    if (Path.GetExtension(file).ToLower() == fileExtension[i]) // Split으로 구분한 확장자들과 비교
                                    {
                                        IEnumerable<string> read = File.ReadLines(file, Encoding.Default); // file을 읽고 read에 한줄씩 저장
                                        foreach (string line in read)
                                        {
                                            // 대소문자 구분없이 line에 txtContnt.Text가 포함되어 있으면
                                            if (Regex.IsMatch(line, content, RegexOptions.IgnoreCase))
                                            {
                                                total++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        SetMax(total);
                        percentCheck = true; // 계산 끝 확인
                    }));
                    t1.Start(); // 쓰레드 시작

                    btnSearch.Text = "검색중...";

                    foreach (string file in files)
                    {
                        // ---- 내용 검색 o, 확장자 검색 x ---- //
                        if (!extensionBool)
                        {
                            int count = 0; // 내용이 있는 파일 행 번호

                            IEnumerable<string> read = File.ReadLines(file, Encoding.Default); // file을 읽고 read에 한줄씩 저장
                            foreach (string line in read) // 한 줄마다 반복
                            {
                                if (searchStop) // 검색을 중지 했을 경우 멈춤
                                    break;
                                locker.WaitOne(Timeout.Infinite); // 대기 신호를 받을 때 현재 위치에서 무한 대기(재시작할 때 여기부터)
                                count++; // 행 번호 증가
                                
                                // 대소문자 구분없이 line에 txtContnt.Text가 포함되어 있으면
                                if (Regex.IsMatch(line, content, RegexOptions.IgnoreCase))
                                {
                                    ListViewItem item = new ListViewItem(Path.GetFileName(file)); // 파일명
                                    item.SubItems.Add(Path.GetFullPath(file)); // 파일 경로
                                    item.SubItems.Add(count.ToString()); // 행번호
                                    item.SubItems.Add(line); // 내용
                                    listView1.Items.Add(item); // line만큼 리스트뷰 등록
                                    ++cnt;
                                    label4.Text = cnt + " / " + total;

                                    if (percentCheck) // Maximum값 구하는 쓰레드가 완료 되었을 때
                                    {
                                        pct = ((cnt * 100) / total); // 퍼센트 구하기 -> (현재cnt * 100) / total
                                        backgroundWorker1.ReportProgress(pct); // 프로그레스바 진행도를 pct로 변경 (backgroundWorker1_ProgressChanged 이벤트 발생)
                                        label6.Text = pct + " %";
                                    }

                                    listView1.EnsureVisible(listView1.Items.Count - 1); // 스크롤 제일 아래로 내리기
                                }
                            }
                        }
                        // ---- 내용 검색 o, 확장자 검색 o ---- //
                        else
                        {
                            for (int i = 0; i < fileExtension.Count; i++) // 확장자 개수만큼 반복
                            {
                                if (Path.GetExtension(file).ToLower() == fileExtension[i]) // Split으로 구분한 확장자들과 비교
                                {
                                    int count = 0; // 내용이 있는 파일 행 번호

                                    IEnumerable<string> read = File.ReadLines(file, Encoding.Default); // file을 읽고 read에 한줄씩 저장
                                    foreach (string line in read) // 한 줄마다 반복
                                    {
                                        if (searchStop) // 검색을 중지 했을 경우 멈춤
                                            break;
                                        locker.WaitOne(Timeout.Infinite); // 대기 신호를 받을 때 현재 위치에서 무한 대기(재시작할 때 여기부터)
                                        count++; // 행 번호 증가

                                        // 대소문자 구분없이 line에 txtContnt.Text가 포함되어 있으면
                                        if (Regex.IsMatch(line, content, RegexOptions.IgnoreCase))
                                        {
                                            ListViewItem item = new ListViewItem(Path.GetFileName(file)); // 파일명
                                            item.SubItems.Add(Path.GetFullPath(file)); // 파일 경로
                                            item.SubItems.Add(count.ToString()); // 행번호
                                            item.SubItems.Add(line); // 내용
                                            listView1.Items.Add(item); // line만큼 리스트뷰 등록
                                            ++cnt;
                                            label4.Text = cnt + " / " + total;

                                            if (percentCheck) // Maximum값 구하는 쓰레드가 완료 되었을 때
                                            {
                                                pct = ((cnt * 100) / total); // 퍼센트 구하기 -> (현재cnt * 100) / total
                                                backgroundWorker1.ReportProgress(pct); // 프로그레스바 진행도를 pct로 변경 (backgroundWorker1_ProgressChanged 이벤트 발생)
                                                label6.Text = pct + " %";
                                            }
                                            listView1.EnsureVisible(listView1.Items.Count - 1); // 스크롤 제일 아래로 내리기
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else // 내용 입력 x
                {
                    // ---- progressBar Maximum 값 구하기위한 파일 총 개수 ---- //
                    int total = 0; // 파일 100%일 때 개수
                    int cnt = 0; // 검색하고 있는 파일 개수
                    int pct = 0; // 계산된 퍼센트 
                    bool percentCheck = false; // 쓰레드 계산 끝나는 것을 확인하기 위한 bool 값

                    t1 = new Thread(new ThreadStart(delegate ()
                    {
                        foreach (string file in files)
                        {
                            if (searchStop || backgroundWorker1.CancellationPending) // 검색 중지하면 멈춤
                                break;
                            locker.WaitOne(Timeout.Infinite); // 대기 신호를 받을 때 현재 위치에서 무한 대기(재시작할 때 여기부터)
                            // ---- 내용 검색 x, 확장자 검색 x ---- //
                            if (!extensionBool)
                            {
                                total++;
                            }
                            // ---- 내용 검색 x, 확장자 검색 o ---- //
                            else
                            {
                                for (int i = 0; i < fileExtension.Count; i++) // 확장자 개수만큼 반복
                                {
                                    if (Path.GetExtension(file).ToLower() == fileExtension[i]) // Split으로 구분한 확장자들과 비교
                                    {
                                        total++;
                                    }
                                }
                            }
                        }
                        SetMax(total);
                        percentCheck = true; // 계산 끝 확인
                    }));
                    t1.Start(); // 쓰레드 시작

                    btnSearch.Text = "검색중...";

                    foreach (string file in files)
                    {
                        // ---- 내용 검색 x, 확장자 검색 x ---- //
                        if (!extensionBool)
                        {
                            if (searchStop || backgroundWorker1.CancellationPending) // 검색을 중지 했을 경우 멈춤
                                break;
                            locker.WaitOne(Timeout.Infinite); // 대기 신호를 받을 때 현재 위치에서 무한 대기(재시작할 때 여기부터)
                            ListViewItem item = new ListViewItem(Path.GetFileName(file)); // 파일명
                            item.SubItems.Add(Path.GetFullPath(file)); // 파일 경로
                            listView1.Items.Add(item);
                            ++cnt;
                            label4.Text = cnt + " / " + total;

                            if (percentCheck) // Maximum값 구하는 쓰레드가 완료 되었을 때
                            {
                                pct = ((cnt * 100) / total); // 퍼센트 구하기 -> (현재cnt * 100) / total
                                backgroundWorker1.ReportProgress(pct); // 프로그레스바 진행도를 pct로 변경 (backgroundWorker1_ProgressChanged 이벤트 발생)

                                label6.Text = pct + " %";
                            }

                            listView1.EnsureVisible(listView1.Items.Count - 1); // 스크롤 제일 아래로 내리기
                        }
                        // ---- 내용 검색 x, 확장자 검색 o ---- //
                        else
                        {
                            for (int i = 0; i < fileExtension.Count; i++) // 확장자 개수만큼 반복
                            {
                                if (searchStop) // 검색을 중지 했을 경우 멈춤
                                    break;
                                locker.WaitOne(Timeout.Infinite); // 대기 신호를 받을 때 현재 위치에서 무한 대기(재시작할 때 여기부터)
                                if (Path.GetExtension(file).ToLower() == fileExtension[i]) // Split으로 구분한 확장자들과 비교
                                {
                                    ListViewItem item = new ListViewItem(Path.GetFileName(file)); // 파일명
                                    item.SubItems.Add(Path.GetFullPath(file)); // 파일 경로
                                    listView1.Items.Add(item);
                                    ++cnt;
                                    label4.Text = cnt + " / " + total;

                                    if (percentCheck) // Maximum값 구하는 쓰레드가 완료 되었을 때
                                    {
                                        pct = ((cnt * 100) / total); // 퍼센트 구하기 -> (현재cnt * 100) / total
                                        backgroundWorker1.ReportProgress(pct); // 프로그레스바 진행도를 pct로 변경 (backgroundWorker1_ProgressChanged 이벤트 발생)
                                        label6.Text = pct + " %";
                                    }

                                    listView1.EnsureVisible(listView1.Items.Count - 1); // 스크롤 제일 아래로 내리기
                                }
                            }
                        }
                    }
                }
            }

            // 예외처리
            catch (UnauthorizedAccessException eu) // 엑세스 거부시
            {
                label4.Text = "검색 중지...";
                searchStart();
                MessageBox.Show("Error : UnauthorizedAccessException \n " + eu.Message);
            }
            catch (ArgumentException ea) // 수량자 관련
            {
                label4.Text = "검색 중지...";
                searchStart();
                MessageBox.Show("Error : ArgumentException \n " + ea.Message);
            }
            catch (IOException ei) // 입출력 문제
            {
                label4.Text = "검색 중지...";
                searchStart();
                MessageBox.Show("Error : IOException \n " + ei.Message);
            }
            catch (Exception ex) // 기타
            {
                label4.Text = "검색 중지...";
                searchStart();
                MessageBox.Show("Error - Exception \n " + ex.Message);
            }
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 백그라운드 작업 진행률 표시
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage; // 프로그레스 바의 값을 받아온 퍼센트 값으로 변경
        }

        // ---------------------------------------------------------------------------------------------------------- //
        // 백그라운드 작업이 완료되면
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // 결과 표시
            TimeSpan ts = DateTime.Now - startTime; // 현재 시간 - 처음 검색 시간
            string resultTime = ts.ToString("mm':'ss'.'fff"); // 00:00.000 초

            if (!searchStop) // 검색 중지가 되지않고 끝까지 검색 완료
            {
                btnStop.Text = "검색 중지";
                label4.Text = "검색 완료. (" + listView1.Items.Count + "개의 결과 / " + resultTime + " 초)";
                progressBar1.Value = progressBar1.Maximum;
                label6.Text = "100 %";
            }
            else // 검색 중지
            {
                label4.Text = "검색 중지. (" + listView1.Items.Count + "개의 결과 / " + resultTime + " 초)";
            }
            searchStart(); // 버튼 Enabled 활성화
        }
        // ---------------------------------------------------------------------------------------------------------- //
        
    }
}