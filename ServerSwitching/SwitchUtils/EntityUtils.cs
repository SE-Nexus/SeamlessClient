using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeamlessClient.ServerSwitching.SwitchUtils
{
    public static class EntityUtils
    {


        public static void RemoveOldClientEntities()
        {
            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent is MyPlanet)
                {
                    //Re-Add planet updates 
                    MyEntities.RegisterForUpdate(ent);
                    continue;
                }

                ent.Close();
            }
        }



    }
}
