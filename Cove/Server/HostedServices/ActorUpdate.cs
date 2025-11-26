/*
   Copyright 2024 DrMeepso

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/


using System;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace Cove.Server.HostedServices
{

    public class ActorUpdateService : IHostedService, IDisposable
    {
        private readonly ILogger<ActorUpdateService> _logger;
        private Timer _timer;
        private CoveServer server;

        public ActorUpdateService(ILogger<ActorUpdateService> logger, CoveServer server)
        {
            _logger = logger;
            this.server = server;
        }

        // This method is called when the service is starting.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Actor_Update_Service is up.");

            // Setup a timer to trigger the task periodically.
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(1) / 12);

            return Task.CompletedTask;
        }

        int idelUpdateCount = 30;
        Dictionary<long, Vector3> pastTransforms = new Dictionary<long, Vector3>();
        int updateI = 0;

        // This is the method that will be triggered periodically by the timer.
        private void DoWork(object state)
        {
            updateI++;


            try
            {

                foreach (PluginInstance plugin in server.loadedPlugins)
                {
                    plugin.plugin.onUpdate();
                }

                lock (server.serverActorListLock)
                {
                    foreach (WFActor actor in server.serverOwnedInstances.ToList())
                    {
                        actor.onUpdate();

                        if (!pastTransforms.ContainsKey(actor.InstanceID))
                        {
                            pastTransforms[actor.InstanceID] = Vector3.Zero;
                        }

                        if (actor.pos != pastTransforms[actor.InstanceID] || (updateI == idelUpdateCount))
                        {

                            Dictionary<string, object> packet = new Dictionary<string, object>();
                            packet["type"] = "actor_update";
                            packet["actor_id"] = actor.InstanceID;
                            packet["pos"] = actor.pos;
                            packet["rot"] = actor.rot;

                            pastTransforms[actor.InstanceID] = actor.pos; // crude

                            server.sendPacketToPlayers(packet);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                //Log(e);
                // just means the list was modified while iterating
                // most likly a actor was added or removed because of a spawn or despawn
                // nothing to worry about
            }

            if (updateI >= idelUpdateCount)
            {
                updateI = 0;
            }

        }

        // This method is called when the service is stopping.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ActorUpdateService is stopping.");

            // Stop the timer and dispose of it.
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        // This method is called to dispose of the resources.
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

}
