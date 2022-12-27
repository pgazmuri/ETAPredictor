# ETAPredictor

Predict ETA of vehicles as they travel to stops on routes that generally repeat. Suitable for busses/shuttles on campuses, at airports, etc...

Provide the location of known stops, along with live (or historical) GPS data from vehicles, and the model will provide a table of ETAs for each stop.

The model is intended to be very lightweight in terms of compute, eschewing processing heavy AI for a simple statistical approach based on historical ETA based on current vehicle location.

It also takes into account current speeds compared to historical speeds so that things like traffic are (naively) taken into account. Data older than 5 days is automatically purged so the model does not consume mode and more memory over time.

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
{StopName: "Bus Stop 1", ETA: 5 min, Serial "Bus 1"},
{StopName: "Bus Stop 2", ETA: 10 min, Serial "Bus 1"},
]