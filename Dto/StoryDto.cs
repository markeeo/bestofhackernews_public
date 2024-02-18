using System.Runtime.Serialization;

namespace BestOfHackerNews.Dto
{
    [DataContract]
    public class StoryDto
    {
        [DataMember]
        public string title {  get; set; } =string.Empty;

        [DataMember]
        public string uri { get; set; } = string.Empty;

        [DataMember]
        public string postedBy { get; set; } = string.Empty;

        [DataMember]
        public string time { get; set; } = string.Empty;

        [DataMember]
        public int score { get; set; }

        [DataMember]
        public int commentCount { get; set; }

        /*
        "title": "A uBlock Origin update was rejected from the Chrome Web Store",
        "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        "postedBy": "ismaildonmez",
        "time": "2019-10-12T13:43:01+00:00",
        "score": 1716,
        "commentCount": 572
        */
    }
}
