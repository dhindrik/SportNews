using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Azure.CognitiveServices.Personalizer;
using Microsoft.Azure.CognitiveServices.Personalizer.Models;
using Xamarin.Forms;

namespace SportNews
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(true)]
    public partial class MainPage : ContentPage
    {
        private const string endpoint = "https://westeurope.api.cognitive.microsoft.com/";
        private const string apiKey = "{key}";

        private PersonalizerClient client;
        private List<Item> source;

        private string eventId;

        public MainPage()
        {
            InitializeComponent();

            Device.BeginInvokeOnMainThread(async () => await Init());

            client = new PersonalizerClient(new ApiKeyServiceClientCredentials(apiKey));
            client.Endpoint = endpoint;

            News.ItemTapped += News_ItemTapped;
        }

        private async void News_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            var reward = 0.0;

            if (e.ItemIndex < 3)
                reward = 1.0;
            else if (e.ItemIndex < 5)
                reward = 0.8;
            else if (e.ItemIndex < 10)
                reward = 0.6;
            else if (e.ItemIndex < 15)
                reward = 0.5;
            else if (e.ItemIndex < 20)
                reward = 0.4;
            else if (e.ItemIndex < 30)
                reward = 0.3;
            else if (e.ItemIndex < 40)
                reward = 0.2;
            else
                reward = 0.1;

            await client.RewardAsync(eventId, reward);

        }

        public async Task Init()
        {
            News.IsVisible = false;
            Loading.IsVisible = true;

            var feeds = new List<System.Threading.Tasks.Task<Feed>>()
           {
               FeedReader.ReadAsync("https://feeds.expressen.se/sport/"),
               FeedReader.ReadAsync("https://www.aftonbladet.se/sportbladet/rss.xml"),
           };

            await Task.WhenAll(feeds);

            var items = new List<FeedItem>();

            foreach(var feed in feeds)
            {
                var result = feed.Result;

                items.AddRange(result.Items);
            }

            var actions = new List<RankableAction>();

            foreach(var item in items.OrderByDescending(x => x.PublishingDate).Take(50))
            {
                string source = null;

                foreach (var feed in feeds)
                {
                    if (feed.Result.Items.Contains(item))
                    {
                        source = feed.Result.Link;
                        break;
                    }
                }

                var features = new List<object>()
                {
                    new {item.Title },
                    new {item.Author },
                    new {item.Description },
                    new {source }
                };

                var action = new RankableAction(item.Id, features);
                actions.Add(action);
            }

            

            string timeOfDay = null;

            if(DateTime.Now.Hour > 22 || DateTime.Now.Hour < 5)
            {
                timeOfDay = "night";
            }
            else if(DateTime.Now.Hour >= 5 && DateTime.Now.Hour < 12)
            {
                timeOfDay = "morning";
            }
            else if(DateTime.Now.Hour >= 12 && DateTime.Now.Hour < 17)
            {
                timeOfDay = "afternoon";
            }
            else
            {
                timeOfDay = "evening";
            }

            var context = new List<object>()
            {
                new {timeOfDay }
            };

            eventId = Guid.NewGuid().ToString();
           
            var rankRequest = new RankRequest()
            {
                Actions = actions,
                ContextFeatures = context,
                ExcludedActions = new List<string>(),
                EventId = eventId
            };

            try
            {

                var rankResult = await client.RankAsync(rankRequest);

                source = new List<Item>();

                foreach (var ranked in rankResult.Ranking.OrderByDescending(x => x.Probability))
                {
                    var feedItem = items.Single(x => x.Id == ranked.Id);

                    var urls = Regex.Matches(feedItem.Description, @"(http|ftp|https):\/\/([\w\-_]+(?:(?:\.[\w\-_]+)+))([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?");

                    var itm = new Item()
                    {
                        FeedItem = feedItem,
                        Image = urls.FirstOrDefault()?.Value
                    };

                    foreach (var feed in feeds)
                    {
                        if (feed.Result.Items.Contains(feedItem))
                        {
                            itm.Source = feed.Result.Link;
                            break;
                        }
                    }

                    source.Add(itm);
                }

                News.ItemsSource = source;
            }
            catch(Exception ex)
            {
                source = new List<Item>();

                foreach (var feedItem in items)
                {
                    var urls = Regex.Matches(feedItem.Description, @"(http|ftp|https):\/\/([\w\-_]+(?:(?:\.[\w\-_]+)+))([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?");

                    var itm = new Item()
                    {
                        FeedItem = feedItem,
                        Image = urls.FirstOrDefault()?.Value
                    };

                    foreach(var feed in feeds)
                    {
                        if(feed.Result.Items.Contains(feedItem))
                        {
                            itm.Source = feed.Result.Link;
                            break;
                        }
                    }

                    source.Add(itm);
                }

                News.ItemsSource = source;
            }

            News.ScrollTo(source.First(), ScrollToPosition.Start, false);
            News.IsVisible = true;
            Loading.IsVisible = false;

        }

        private void Handle_Clicked(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(async () => await Init());
        }

    }

    public class Item
    {
        public FeedItem FeedItem { get; set; }
        public string Image { get; set; }
        public string Source { get; set; }
    }
}
