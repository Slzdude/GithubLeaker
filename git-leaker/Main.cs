using System;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Collections.Generic;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Octokit;
using FastColoredTextBoxNS;
using System.Drawing;

namespace git_leaker
{
    public partial class Main : Form
    {
        private FlurlClient client = new FlurlClient("https://api.github.com/");
        private Dictionary<string, string> headers = new Dictionary<string, string> { { "Accept", "*/*" }, { "User-Agent", "Flurl" } };
        private int hightLightStyleIndex = 0;
        public Main()
        {
            InitializeComponent();
            FlurlHttp.Configure(settings =>
            {
                settings.HttpClientFactory = new ProxyHttpClientFactory("http://127.0.0.1:8080");
            });
            hightLightStyleIndex = tbLog.AddStyle(new TextStyle(Brushes.White, Brushes.Green, FontStyle.Bold));
        }

        public static void RunAsync(Action action)
        {
            ((Action)(delegate ()
            {
                action.Invoke();
            })).BeginInvoke(null, null);
        }

        public void RunInMainthread(Action action)
        {
            this.BeginInvoke((Action)(delegate ()
            {
                action.Invoke();
            }));
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            headers["Authorization"] = "token " + tbAPIToken.Text;
            RunAsync(() =>
            {
                RunInMainthread(async () =>
                {
                    var result = await client.WithHeaders(headers).Request("rate_limit").GetStringAsync();
                    JToken parsedJson = JToken.Parse(result);
                    tbLog.Text = parsedJson.ToString(Formatting.Indented);
                });
            });
        }

        private void fetchCommitters(string url)
        {
            var regex = new Regex(@"^https://github.com/([^/]+)/([^/]+)");
            var match = regex.Match(url);
            if (!match.Success)
            {
                MessageBox.Show("无法识别的Github Repo URL", "处理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var user = match.Groups[1].Value;
            var repo = match.Groups[2].Value;
            RunAsync(() =>
            {
                var request = client.WithHeaders(headers).Request(string.Format("repos/{0}/{1}/commits", user, repo));
                RunInMainthread(async () =>
                {
                    var response = await request.GetJsonAsync<List<Commits>>();
                    tbLog.Text = "Name\tEmail\tDate\r\n";
                    foreach (var commit in response)
                    {
                        var committer = commit.commit.committer;
                        tbLog.AppendText(string.Format("{0}\t{1}\t{2}\r\n", committer.name, committer.email, committer.date));
                    }
                });
            });
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            fetchCommitters(tbRepoURL.Text);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            RunAsync(() =>
            {
                RunInMainthread(async () =>
                {
                    var response = await client
                        .WithHeaders(headers)
                        .Request("search/code")
                        .SetQueryParam("q", tbSearchValue.Text)
                        .SetQueryParam("per_page", "100")
                        .WithHeader("Accept", "application/vnd.github.v3.text-match+json")
                        .GetJsonAsync<QuickType.SearchCodeResultWitchTextMatch>();
                    lvSearchResult.Clear();
                    lvSearchResult.Columns.Add("ID", 40, HorizontalAlignment.Center);
                    lvSearchResult.Columns.Add("Name", 100, HorizontalAlignment.Center);
                    lvSearchResult.Columns.Add("Path", 200, HorizontalAlignment.Center);
                    lvSearchResult.Columns.Add("Repo", 100, HorizontalAlignment.Center);
                    lvSearchResult.Columns.Add("Score", 80, HorizontalAlignment.Center);
                    int i = 0;
                    foreach (var item in response.items)
                    {
                        i += 1;
                        var listViewItem = new ListViewItem(new string[]
                        {
                            i.ToString(), item.name, item.path, item.repository.html_url,item.score.ToString()
                        });
                        listViewItem.Tag = item.text_matches;
                        lvSearchResult.Items.Add(listViewItem);
                    }
                });
            });
        }

        private void lvSearchResult_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lvSearchResult.SelectedItems.Count > 0)
            {
                fetchCommitters(lvSearchResult.SelectedItems[0].SubItems[3].Text);
            }
        }

        private void lvSearchResult_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvSearchResult.SelectedItems.Count > 0)
            {
                var text_matches = (List<QuickType.TextMatch>)lvSearchResult.SelectedItems[0].Tag;
                tbLog.Text = "";
                foreach (var text_match in text_matches)
                {
                    var startLine = tbLog.Text.Split('\n').Length;
                    tbLog.AppendText(text_match.fragment + "\r\n");
                    foreach (var match in text_match.matches)
                    {
                        var range = new FastColoredTextBoxNS.Range(tbLog, new Place(match.indices[0], startLine - 1), new Place(match.indices[1], startLine - 1));
                        range.SetStyle(tbLog.Styles[hightLightStyleIndex]);
                    }
                }
            }
        }
    }
}
