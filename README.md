# ETAPredictor

Predict ETA of vehicles as they travel to stops on routes that generally repeat. Suitable for busses/shuttles on campuses, at airports, etc...

Provide the location of known stops, along with live (or historical) GPS data from vehicles, and the model will provide a table of ETAs for each stop.

The model is intended to be very lightweight in terms of compute, eschewing processing heavy AI for a simple statistical approach based on historical ETA based on current vehicle location. It knows nothing about roads or traffic and does not rely on any external data feeds. ""Roads?! Where we're going, we don't need [to know about] roads!""

It also takes into account current speeds compared to historical speeds so that things like traffic are (naively) taken into account. Data older than 5 days is automatically purged so the model only grows to a point and does not consume more and more memory over time.

Example:

var stops = new List<GPSData>() { 

	new GPSData() { Latitude = 32.432432, Longitude = -71.2344, IdentifiedAsStopName="Bus Stop 1" },

	new GPSData() { Latitude = 32.342432, Longitude = -71.5444, IdentifiedAsStopName="Bus Stop 2" },

};

var model = new ETAPRedictor.RoutesModel(stops);

//for each data point we recieve, we integrate it into the model:

model.IntegrateDataPointIntoModel(<data from GPS tracker>);

//we can request an ETA table at any time:

model.GetETATable();

Returns:

[

{StopName: "Bus Stop 1", ETA: 5 min, Serial: "Bus 1"},

{StopName: "Bus Stop 2", ETA: 10 min, Serial: "Bus 1"},

]



# Saving the model

The RoutesModel provides LoadFromJSON and SaveToJSON methods, so you can save your model as you integrate more data into it and make your predictions available upon service restart.

Save the JSON to a cloud storage blob or local filesystem.



# Simulation Mode

By default, the model will use the current UTC DateTime in making predictions, assuming that the data you are feeding it is being provided in real time. For testing purposes, you may enable simulation mode:


var model = new ETAPRedictor.RoutesModel(stops, true); //second param sets simulationMode = true


//before integrating a data point, or requesting an ETATable, set the current time of your simulation


model.CurrentTime = <your simulation time>;

model.IntegrateDataPointIntoModel(<point>);



//...


model.CurrentTime = <your simulation time>;

model.GetETATable();



#Known Issues

ETA table will include null times if no statistically relevant data points exist to make a prediction. As you feed more data into the model this should go away. It's best to collect data for a few days and save your model before making the predictions live.

Predictions can jump around a bit... this model makes no attempt at smoothing. It's recommended that you implement some kind of smoothing or averaging of predictions if this will be jarring to your users. You could have a countdown clock that only resets when the predictions become shorter over time, or subtly speeds up or slows down based on moving averages. I believe these choices are best left to the UI and not the backend, so I have no plans to implement smoothing. Reach out to me if you feel differently!.


#Planned improvements

We currently match historical data points based on location alone. Currently it operates by assuming that the most recent data points are most relevant, so if there's a slowdown over a long period of time, the system will adjust.  But a short term slowdown will cause the model to be wrong both as it slows down and as it speeds up again. 


If we incorporated time of day, day of week, speed and other variables into how we match our statistically relevant historical ETAs, the model should become more accurate.  


However, this is meant to be a lightweight in-memory model that does not require a database or significant compute or memory, so to achieve this more sophisticated mode of operation, we will need to begin aggregating statistics within the model... collapsing similar datapoints essentially.


So my next planned steps will be to bucket historical datapoints where the ETA, speed, day and time of day, and position are similar, and then alter the calculations to use those buckets instead.