using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using System.Collections.Generic;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;
using System.IO;
using System.Web;






namespace SentiBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// 
        public virtual async Task<HttpResponseMessage> Post([FromBody] Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            const string emotionApiKey = "9c6bf8e799ad49a4ba09e7b31cd13086";

            //Emotion SDK objects that take care of the hard work
            EmotionServiceClient emotionServiceClient = new EmotionServiceClient(emotionApiKey);
            Emotion[] emotionResult = null;
            Activity reply = null;

            if (activity == null || activity.GetActivityType() != ActivityTypes.Message)
            {
                //add code to handle errors, or non-messaging activities
            }

            //If the user uploaded an image, read it, and send it to the Vision API
            if (activity.Attachments.Any() && activity.Attachments.First().ContentType.Contains("image"))
            {
                //stores image url (parsed from attachment or message)
                string uploadedImageUrl = activity.Attachments.First().ContentUrl; ;
                uploadedImageUrl = HttpUtility.UrlDecode(uploadedImageUrl.Substring(uploadedImageUrl.IndexOf("file=") + 5));

                using (Stream imageFileStream = File.OpenRead(uploadedImageUrl))
                {
                    try
                    {
                        emotionResult = await emotionServiceClient.RecognizeAsync(imageFileStream);
                    }
                    catch (Exception e)
                    {
                        emotionResult = null;
                    }
                }
            }//Else, if the user did not upload an image, determine if the message contains a url, and send it to the Vision API
            else
            {
                try
                {
                    emotionResult = await emotionServiceClient.RecognizeAsync(activity.Text);
                }
                catch (Exception e)
                {
                    emotionResult = null; //on error, reset analysis result to null
                }
            }


            if (emotionResult != null)
            {   
                
                Scores emotionScores = emotionResult[0].Scores;

                //Retrieve list of emotions for first face detected and sort by emotion score (desc)
                IEnumerable<KeyValuePair<string, float>> emotionList = new Dictionary<string, float>()
                {
                    { "angry", emotionScores.Anger},
                    { "contemptuous", emotionScores.Contempt },
                    { "disgusted", emotionScores.Disgust },
                    { "frightened", emotionScores.Fear },
                    { "happy", emotionScores.Happiness},
                    { "neutral", emotionScores.Neutral},
                    { "sad", emotionScores.Sadness },
                    { "surprised", emotionScores.Surprise}
                }
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .ToList();

                KeyValuePair<string, float> topEmotion = emotionList.ElementAt(0);
                string topEmotionKey = topEmotion.Key;
                float topEmotionScore = topEmotion.Value;

                reply = activity.CreateReply("I found a face! I am " + (int)(topEmotionScore * 100) +
                                             "% sure the person seems " + topEmotionKey);
            }
            else
            {
                reply = activity.CreateReply("Could not find a face, or something went wrong. " +
                                                  "Try sending me a photo with a face");
            }
            
            await connector.Conversations.ReplyToActivityAsync(reply);
            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        }


    }
}