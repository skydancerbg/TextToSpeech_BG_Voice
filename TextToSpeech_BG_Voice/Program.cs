using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace TextToSpeech_BG_Voice
{
    // Stefan SRG 27.3.2019
    // There is no Bulgarian Voice in Ubuntu. 
    // As a workaround, we are forced to start a Windows VM
    // and synthesize the speech there, playing it back on the Robco robot speakers
    // using bluetooth connection
    // There is a ROS action server providing the Bulgarian speech functionality,
    // which connects to this code using MQTT messages. 

    class Program
    {
        private static SpeechSynthesizer synth = new SpeechSynthesizer();

        static void Main(string[] args)
        {
            //https://www.nuget.org/packages/M2Mqtt/
            //Install-Package M2Mqtt -Version 4.3.0

            // MQTT client setup
            string BrokerAddress = "192.168.10.107";
            var client = new MqttClient(BrokerAddress);

            // register a callback-function called by the library when a message was received
            client.MqttMsgPublishReceived += client_messageReceived;
            
            var clientId = Guid.NewGuid().ToString();
            // supply - user, pass for the MQTT server
            client.Connect(clientId, "test", "tst");

            // subscribe to the topic "/robco/text_to_speak" with QoS 2
            client.Subscribe(
                new string[] { "/robco_bg_tts/text_to_speak", "/robco_bg_tts/command" },
                new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

            synth.SetOutputToDefaultAudioDevice();

            synth.SelectVoice("Vocalizer Expressive Daria Harpo 22kHz");
        }
        static void client_messageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //For multiple subscribed topics, put an if statement into the message handler to branch based
            //on the incoming messages topic e.Topic.
            //You can write functions to handle the different message types and call these from the message handler
            //and pass the MqttMsgPublishEventArgs object to those functions.

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // handle message received
            ////Console.WriteLine("raw message= " + Encoding.UTF8.GetString(e.Message) + "      on topic " + e.Topic);
            var stringLenght = Encoding.UTF8.GetString(e.Message).Length;
            //The MQTT client in ROS includes ' "��data�", ' infront of the message payload
            //Here we remove ��data� from the message ("��data�", "") and get only the string payload
            string messageText = Encoding.UTF8.GetString(e.Message).Substring(9, stringLenght - 9);
            ////Console.WriteLine("message received= " + Encoding.UTF8.GetString(e.Message).Substring(9, stringLenght - 9));
            if (e.Topic == "/robco_bg_tts/text_to_speak")
            {
                // TODO publish response to the response topic

                //Cancel syntesis if currently speaking something else...
                var current = synth.GetCurrentlySpokenPrompt();
                if (current != null)
                    synth.SpeakAsyncCancel(current);
                // Speak out the string. 
                speak(messageText);
            }
            else if (e.Topic == "/robco_bg_tts/command")
            {
                if (messageText == "cancel")
                {
                    cancelSpeaking();
                }
            }
        }

        private static void cancelSpeaking()
        {
            // cancel if currently speaking
            var current = synth.GetCurrentlySpokenPrompt();
            if (current != null)
                synth.SpeakAsyncCancel(current);

            // TODO publish response to the response topic
            // publish a message on "/home/temperature" topic with QoS 2   
            //client.Publish("/home/temperature", Encoding.UTF8.GetBytes(strValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE);
        }

        private static void speak(string messageText)
        {
            synth.SpeakAsync(messageText);
        }

        void client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            Debug.WriteLine("MessageId = " + e.MessageId + " Published = " + e.IsPublished);
        }
        void client_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            Debug.WriteLine("Subscribed for id = " + e.MessageId);
        }

    }
}
