[nuget]: https://nuget.org/packages/ServerSentEvent4Net
[slf4net]: https://github.com/englishtown/slf4net
[appharbor]: https://ssetest.apphb.com/
ServerSentEvent4Net
===================

ServerSentEvent4Net is a Server-Sent Event implementation for ASP.Net WebApi. ServerSentEvent4Net simplifies the use of Server-Sent Events, see the sample below.
You can test it live [here][appharbor].

##Install##
ServerSentEvent4Net is available as a [Nuget-package][nuget]. From the Package Manager Console enter:
            
            Install-package ServerSentEvent4Net

##How to use?##
In your ApiController you can use the ServerSentEvent as a static member-variable, like this.

        private static readonly Lazy<ServerSentEvent> SSE = new Lazy<ServerSentEvent>(() =>
        {
            var sse = new ServerSentEvent(10);
            return sse;
        });

Then catch the GET-requests to add a subscriber.

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            return SSE.Value.AddSubscriber(request);
        }

And to send a message to all subscribers, simply call Send.

		SSE.Value.Send("My message goes here....");



For a complete sample, see the demo-project!

##Logging##
ServerSentEvent4Net uses [slf4net] as a logging facade.

##ToDo##
- Implement functionallity to only send to subcribers fullfilling a criteria.

##Contributions##
I'll be more than happy to get contributions!!!
