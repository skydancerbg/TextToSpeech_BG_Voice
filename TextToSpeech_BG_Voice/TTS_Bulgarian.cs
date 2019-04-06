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

    class TTS_Bulgarian
    {
        private static SpeechSynthesizer synth = new SpeechSynthesizer();

        static void Main(string[] args)
        {
            //https://www.nuget.org/packages/M2Mqtt/
            //Install-Package M2Mqtt -Version 4.3.0

            // MQTT client setup

            // Change default MQTT broker address here
            string BrokerAddress = "192.168.10.2";

            if (args.Length == 1)
            {
                BrokerAddress = args[0];
                System.Console.WriteLine("Setting broker IP to: " + BrokerAddress);
            }
            else
            {
                System.Console.WriteLine("To change the default broker IP use: TTS_Bulgarian \"MQTT_broker_IP\"");
                System.Console.WriteLine("Setting broker IP to: " + BrokerAddress);
            }


            //var client = new MqttClient(BrokerAddress);
            var client = new MqttClient("192.168.1.2");


            // register a callback-function called by the library when a message was received
            client.MqttMsgPublishReceived += client_messageReceived;
            
            var clientId = Guid.NewGuid().ToString();
            //TODO create new user pass for the MQTT broker adn replace the one below! 
            // supply - user, pass for the MQTT broker
            client.Connect(clientId, "openhabian", "robko123");

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // ROS topics: 
            //        /ttsbg_ros/tts_text
            //        /ttsbg_ros/command
            //        /ttsbg_ros/response    
            // MQTT topics: 
            //          /ttsbg_mqtt/tts_text
            //          /ttsbg_mqtt/command
            //          /ttsbg_mqtt/
            // 
            // mqtt_bridge ROS package is set to transfer the messages between ROS and the MQTT broker in both directions
            // Topics have to be setup in mqtt_bridge package in /home/robcoctrl/catkin_ws/src/mqtt_bridge/config/openhab_tts_params.yaml
            //
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            // Subscribe to the required MQTT topics with QoS 2
            // There is a bug when you specify more than one topic at the same time???
            //https://stackoverflow.com/questions/39917469/exception-of-type-uplibrary-networking-m2mqtt-exceptions-mqttclientexception-w
            //client.Subscribe(
            //    new string[] { "/ttsbg_mqtt/tts_text", "/ttsbg_mqtt/command" },
            //    new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            client.Subscribe(new string[] { "/ttsbg_mqtt/tts_text" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            //client.Subscribe(new string[] { "/ttsbg_mqtt/command" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            synth.SetOutputToDefaultAudioDevice();

            //synth.SelectVoice("Vocalizer Expressive Daria Harpo 22kHz");
            synth.SelectVoice("VE_Bulgarian_Daria_22kHz");

            //synth.SelectVoice("Microsoft Zira Desktop");
        }
        static void client_messageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //For multiple subscribed topics, put an if statement into the message handler to branch based
            //on the incoming messages topic e.Topic.
            //You can write functions to handle the different message types and call these from the message handler
            //and pass the MqttMsgPublishEventArgs object to those functions.

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Handle the received message
             
            ////Console.WriteLine("raw message= " + Encoding.UTF8.GetString(e.Message) + "      on topic " + e.Topic);
            var stringLenght = Encoding.UTF8.GetString(e.Message).Length;

            //The MQTT client in ROS includes ' "��data�", ' infront of the message payload
            //Here we remove 7 symbols - ��data� from the message ("��data�", "") and get only the string payload
            string messageText = Encoding.UTF8.GetString(e.Message).Substring(9, stringLenght - 9);
            Console.WriteLine("Text to be spoken received => " + Encoding.UTF8.GetString(e.Message).Substring(9, stringLenght - 9));

            //Branch based on the incoming messages topic e.Topic.
            if (e.Topic == "/ttsbg_mqtt/tts_text")
            {
                //Cancel syntesis if currently speaking something else...
                var current = synth.GetCurrentlySpokenPrompt();
                if (current != null)
                    synth.SpeakAsyncCancel(current);
                // Speak out the string. 
                speak(messageText);
                //TODO
                // Publish started speaking response to the MQTT response topic
                //string strResponse = "speaking";
                //client.Publish("/tts_bg_mqtt/command", Encoding.UTF8.GetBytes(strResponse), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE);

            }
            else if (e.Topic == "/ttsbg_mqtt/command")
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

            // TODO publish started speaking response to the response topic
            // strResponse = "speaking";
            //client.Publish("/tts_bg_mqtt/command", Encoding.UTF8.GetBytes(strValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE);

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
