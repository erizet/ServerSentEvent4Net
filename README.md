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

##Contributions##
I'll be more than happy to get contributions!!!

##License##

    Copyright 2013 Erik Zetterqvist
    
    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at
    
    http://www.apache.org/licenses/LICENSE-2.0
    
    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
