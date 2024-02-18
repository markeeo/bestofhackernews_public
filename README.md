# Overview

BestOfHackerNews is a REST service that retrieves up to 200  of the best HackerNews stories in JSON format. 

The 200 story limitiation matches the maximum available from the HackerNews https://hacker-news.firebaseio.com/v0/beststories.json enpoint 

Once running the REST API endpoint for retrieving stories is *https://localhost:5001/api/BestStories[?count=n]* where n is the number of stories required. 
Omitting the *count* query string option will retrieve all available stories. Having *n* as 0 or less, or greater than 200 will do the same.

# Method

The service uses a local list of the most recent best stories to serve user requests. 

This list is updated at configurable intervals.

This avoids having to call into the HackerNews API for each user request and the use of lock free and thread safe strategies allows for the service to support a high volume of request.


The load on the Hacker news is predetermined by the configuration which consists of the refresh frequency and the number of concurrent tasks used in each refresh cycle. Therefore the freshness of the local list can be tuned based on the system requirements.  This also keeps the load on the HackerNews at a manageable and constant rate

*E.g. The refresh frequency can be set to 1sec, 5secs, 10secs, 30secs, etc.*

*The speed of each refresh is dermined by the number of conurrent tasks used. In tests, 1 task refreshed 200 stories in 30 seconds, 5 tasks took approx 5 secs and  10 tasks took approx 3 seconds.*

Both settings  will determine how close to real time the changes in HackerNews are reflected in the local list



# How to build

- Clone the repository
- Open BestOfHackerNews.sln Solution in Visual Studio (Originally written in VS2022)
- Build solution
  * Note Requires .net 6 core or higher and solution requires access to the nuget.org repository

# How to run

BestOfHackerNews will run as a console application. 

- Once built, navigate to the binary directory ```<root>\bestofhackernews\bin\Release\net6.0``` (change your build config and net version accordingly)
- Run BestOfHackerNews.exe
- Wait for initialisation to complete. This is indicated by a ```Story Manager Ready``` line in the console output
- Navigate to *https://localhost:5001/api/BestStories* in your browser (or http client tool)
 
 ![Startup screen](./startup.png?raw=true "Title")

 # Configuration
 
 Configuration is contained in an appsetting.json file. The less obvious entries are commented below
 ```java
 {
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore.Hosting": "Error",
        "Microsoft": "Information",
        "System": "Information"
      },
      "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ]
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}}] ({SourceContext}) {Message}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "C:/etc/HackerNews/bin/Debug/net8.0/top_hacker_news.txt",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({SourceContext}) {Message}{NewLine}{Exception}"
        }
      }
    ],
    
  },
  "AllowedHosts": "*",
  "AppSettings": {
    "BestStoriesUrl": "https://hacker-news.firebaseio.com/v0/beststories.json",
    "StoryUrl": "https://hacker-news.firebaseio.com/v0/item/{0}.json",
    "StoriesRefreshFrequency": "00:00:30",  //How often to refresh the stories list from HackerNews
    "StartupTimeout": "00:00:30",           //How long to allow for the startup initialisation to complete
    "NumLoaderTasks": 10,                   //Number of concurrent tasks for fetching the latest stories from HackerNews
    "MaxMissingStoryTolerance":  0.25       //Percentage of failures allowed when fetching the latest stores. E.g at 0.25, proceed if at least 150 of 200 stories were loaded correctly

  }
}
 ```


# Scope for improvement

- It is inefficient to reload the entire set of stories from HackerNews. An event based model would be preferable (using the firebase API maybe) so that reloads are limited specifically to relevant stories. Another option would be to use HackerNews' update endpoint ( *https://hacker-news.firebaseio.com/v0/updates.json*) to track changes and only reload changed stories. However both options depend on how and what updates are provided by the HackerNews API

- Another imporovement would be to include unit tests which have been omitted due to time constraints
  
- In the real world, such an system will not run as a windows console application so making it a service or configured to run under a web server would be required

- Another real world improvement would be to expose the service on an externally reachable address i.e. not *localhost*

- The StoryManager responsible for holding and refreshing the stories list is embedded in the service. In a high traffic environment, this can be split out into a microservice that updates a distributed set of public facing Rest API services with the latest list of stories




