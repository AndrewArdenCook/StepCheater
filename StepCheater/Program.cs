using Google.Apis.Auth.OAuth2;
using Google.Apis.Fitness.v1;
using Google.Apis.Fitness.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace StepCheater
{
    class Program
    {
        static void Main(string[] args)
        {
            GoogleFitImport.InsertToGoogleFit();
        }
    }

    public class GoogleFitImport
    {
        private static readonly DateTime zero = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToJavaNanoseconds(DateTime dt)
        {
            var ms = (long) (dt - zero).TotalMilliseconds;
            return  ms * 1000000;
        }

        public static void InsertToGoogleFit()
        {
            var UserId = "me";

            //  https://www.googleapis.com/auth/fitness.body.write
            var clientId = "1019669906028-3qt0u8jf9nqk59vbiq130t295mn5t6dq.apps.googleusercontent.com"; // From https://console.developers.google.com
            var clientSecret = "Sm64S3DFIU1S0-a-44BsCJod"; // From https://console.developers.google.com

            //Scopes for use with the Google Drive API
            string[] scopes = new string[]
            {
                FitnessService.Scope.FitnessActivityRead,
                FitnessService.Scope.FitnessActivityWrite
            };

            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync
            (
                new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                scopes,
                Environment.UserName,
                CancellationToken.None,
                new FileDataStore("Google.Fitness.Auth", false)
            ).Result;

            var fitnessService = new FitnessService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            });


            var dataSource = new DataSource()
            {
                Type = "derived",
                DataStreamName = "StepCheaterDataSource",
                Application = new Google.Apis.Fitness.v1.Data.Application()
                {
                    Name = "StepCheater",
                    Version = "1"
                },
                DataType = new DataType()
                {
                    Name = "com.google.step_count.delta",
                    Field = new List<DataTypeField>()
                {
                    new DataTypeField() {Name = "steps", Format = "integer"}
                }
                },
                Device = new Device()
                {
                    Type = "tablet",
                    Manufacturer = "manf",
                    Model = "model-a",
                    Uid = "1000001",
                    Version = "1.0"
                }
            };

            //https://developers.google.com/fit/rest/v1/data-sources
            //https://developers.google.com/fit/rest/v1/reference/users/dataSources/datasets/get
            //var dataSourceId = $"{dataSource.Type}:{dataSource.DataType.Name}:{clientId.Split('-')[0]}:{dataSource.Device.Manufacturer}:{dataSource.Device.Model}:{dataSource.Device.Uid}:{dataSource.DataStreamName}";
            var dataSourceId = $"{dataSource.Type}:{dataSource.DataType.Name}:{dataSource.Device.Manufacturer}:{dataSource.Device.Model}:{dataSource.Device.Uid}:{dataSource.DataStreamName}";
            try
            { 
                var googleDataSource = fitnessService.Users.DataSources.Get(UserId, dataSourceId).Execute();
            }
            catch (Exception ex) //create if not exists
            {
                var googleDataSource = fitnessService.Users.DataSources.Create(dataSource, UserId).Execute();
            }

            var startNanos = ToJavaNanoseconds(DateTime.UtcNow.Add(new TimeSpan(-1, -10, 0)));//1 hour 10 min ago
            var endNanos = ToJavaNanoseconds(DateTime.UtcNow.Add(new TimeSpan(0, -10, 0)));//10 min ago
            var stepsDataSource = new Google.Apis.Fitness.v1.Data.Dataset()
            {
                DataSourceId = dataSourceId,
                Point = new List<DataPoint>()
                {
                    new DataPoint()
                    {
                        DataTypeName = "com.google.step_count.delta",
                        StartTimeNanos = startNanos,
                        EndTimeNanos = endNanos,
                        Value = new List<Value>()
                        {
                            new Value()
                            {
                                IntVal = 10000
                            }
                        }
                    }
                },
                MinStartTimeNs = startNanos,
                MaxEndTimeNs = endNanos,
        };

            var dataSetId = $"{startNanos}-{endNanos}";
            var save = fitnessService.Users.DataSources.Datasets.Patch(stepsDataSource, UserId, dataSourceId, dataSetId).Execute();
        }
    }
}