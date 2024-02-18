using AutoMapper;
using BestOfHackerNews.Dto;
using System;
using System.Collections.Concurrent;
using System.Text.Json;


namespace BestOfHackerNews
{
    public interface IStoryManager
    {
        List<StoryDto> GetStories(int count);
        Task Start();
        void Stop();

    }


    public class StoryManager : IStoryManager, IDisposable
    {
        //Use concurrent for lock-free thread safety
        ConcurrentQueue<StoryDto> _currentStories;

        DateTime _lastLoadTime;


        ILogger<StoryManager> _logger;

        private readonly AppSettings _appSettings;
        private readonly IMapper _mapper;
        private Timer _refreshTimer;
        public StoryManager(AppSettings appSettings, IMapper mapper, ILogger<StoryManager> logger)
        {
            _appSettings = appSettings;
            _logger = logger;
            _mapper = mapper;
            _refreshTimer = null;
            _currentStories = null;
            _lastLoadTime=default(DateTime);
        }

        public void Dispose()
        {
            Stop();
        }

        public async Task Start()
        {
            //Only initialise once
            if (_currentStories != null) return;

            //Do first initialisation
            await RefreshTopStories();

            //Start refresh timer
            _refreshTimer = new Timer(state => RefreshTopStories(), null, (int)_appSettings.StoriesRefreshFrequency.TotalMilliseconds, (int)_appSettings.StoriesRefreshFrequency.TotalMilliseconds);
        }

        public void Stop()
        {
            try
            {
                //Release timer
                var timer = _refreshTimer;
                _refreshTimer = null;
                if (timer != null) timer.Dispose(); ;

            }
            catch (Exception ex)
            {
                //Not much to do here
            }
        }
        public List<StoryDto> GetStories(int count)
        {
            if (_currentStories == null) throw new InvalidDataException("Stories not initialized");

            //Make thread safe copy
            var copy = _currentStories.ToArray();

            //Return the number of stories requested if within range or everything available
            int numStories = (count > 0 && count <= copy.Length) ? count : copy.Length;

            //Return all available
            List<StoryDto> ret = copy.Take(numStories).ToList();
            return ret;
        }

        public async Task RefreshTopStories()
        {
            try
            {
                //Get current list of best story Ids
                using HttpClient client = new HttpClient();
                DateTime start = DateTime.Now;
                List<int> storyIds = await HttpGet<List<int>>(client, _appSettings.BestStoriesUrl);
                _logger.LogInformation($"Loaded {storyIds?.Count} best story ids in {(DateTime.Now - start).TotalSeconds} seconds");

                //Load up the stories
                IEnumerable<StoryDto> newStories;
                if (_appSettings.NumLoaderTasks <= 1)
                {
                    //Load stories sequentially
                    List<StoryDto> tmpNewStories = new List<StoryDto>();
                    foreach (int storyId in storyIds)
                    {
                        tmpNewStories.Add(await LoadStory(client, storyId));
                    }
                    newStories = tmpNewStories;
                }
                else
                {
                    newStories = await LoadAllStoriesInTasks(storyIds).ConfigureAwait(false);
                }



                _logger.LogInformation($"Loaded {newStories.Count()} stories in {(DateTime.Now - start).TotalSeconds} seconds");

                //Check percentage errored and decide if loaded remainder is  sufficient
                double percentageMissing = 1 - (newStories.Count() / storyIds.Count);
                if (percentageMissing > _appSettings.MaxMissingStoryTolerance)
                {
                    _logger.LogWarning($"{percentageMissing * 100}% of stories requested are missing- Higher than the  {_appSettings.MaxMissingStoryTolerance * 100}% tolerance level. Discarding latest set of stories");
                    return;
                }

                //Replace current stories list. Sort first as concurrent bag is unordered
                _currentStories = new ConcurrentQueue<StoryDto>(newStories.OrderByDescending(s => s.score));
                _lastLoadTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to refresh top stories. {ex.Message}");
            }

        }


        public async Task<ConcurrentBag<StoryDto>> LoadAllStoriesInTasks(List<int> storyIds)
        {

            //Set up a blocking collection of story ids
            BlockingCollection<int> storyIdQueue = new BlockingCollection<int>();
            storyIds.ForEach(id => storyIdQueue.Add(id));
            storyIdQueue.CompleteAdding();


            //Pass story Id's and a destination collection to all tasks 
            Task[] tasks = new Task[_appSettings.NumLoaderTasks];
            ConcurrentBag<StoryDto> loadedStories = new ConcurrentBag<StoryDto>();
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () => await LoadStoriesTaskMethod(storyIdQueue, loadedStories));
            }

            //Wait for all tasks to complete
            await Task.WhenAll(tasks);


            return loadedStories;
        }

        async Task LoadStoriesTaskMethod(BlockingCollection<int> storyIds, ConcurrentBag<StoryDto> stories)
        {
            int taskNum = Task.CurrentId.GetValueOrDefault();
            _logger.LogInformation($"LoadStoriesTaskMethod {taskNum}- Started");

            //Load stories until their are none left
            using HttpClient client = new HttpClient();
            while (!storyIds.IsCompleted)
            {
                
                try
                {
                    int storyId = storyIds.Take();
                    stories.Add(await LoadStory(client, storyId));
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning($"LoadStoriesTaskMethod {taskNum}- All stories processed. Terminating refresh task.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"LoadStoriesTaskMethod {taskNum}- error when attempting to a fetch story. {ex.Message}");
                }

            }

            _logger.LogInformation($"LoadStoriesTaskMethod {taskNum}- Completed");

        }

        async Task<StoryDto> LoadStory(HttpClient client, int storyId)
        {
            string storyUrl = string.Format(_appSettings.StoryUrl, storyId);//$"https://hacker-news.firebaseio.com/v0/item/{storyId}.json";
            HackerNewsStoryDto hackerNewsDto = await HttpGet<HackerNewsStoryDto>(client, storyUrl).ConfigureAwait(false);
            StoryDto storyDto = _mapper.Map<StoryDto>(hackerNewsDto);
            return storyDto;
        }
        async Task<T> HttpGet<T>(HttpClient client, string url)
        {
            try
            {
                HttpResponseMessage resp = await client.GetAsync(url);
                T data = await ReadResponseJSON<T>(resp).ConfigureAwait(false);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"HttpGet error error when attempting to fetch {typeof(T).Name} from {url}. {ex.Message}");
                throw ex;
            }
        }
        async Task<T> ReadResponseJSON<T>(HttpResponseMessage resp)
        {
            try
            {
                if (!resp.IsSuccessStatusCode) throw new ApplicationException($"HTTP Request failed with status code {resp.StatusCode}."); //TODO: Better error

                using Stream jsonStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                T data = await JsonSerializer.DeserializeAsync<T>(jsonStream).ConfigureAwait(false);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get or processes Http response. {ex.Message}");
                throw ex;
            }
        }



    }
}
