namespace BestOfHackerNews
{
    public class AppSettings
    {
        public string BestStoriesUrl { get; set; }
        public string StoryUrl { get; set;}
        public TimeSpan StartupTimeout { get; set; }
        public TimeSpan StoriesRefreshFrequency { get; set;}
        public int NumLoaderTasks { get; set; }
        public double MaxMissingStoryTolerance { get; set; }

    }
}
