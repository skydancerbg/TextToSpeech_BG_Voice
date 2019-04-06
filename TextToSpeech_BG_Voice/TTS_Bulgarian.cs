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



    //#     #################################
    //#     # 
    //#     # Created by Stefan 
    //#     # SRG - Service Robotics Group Bulgaria
    //#     # Version 1.0 from Apr. 6th, 2019.
    //#     #
    //#     #################################
    //#
    //#     There is no good Bulgarian Voice in Ubuntu. 
    //#     As a workaround, we are forced to start a Windows VM
    //#     and synthesize the speech there, playing it back on the Robco robot speakers
    //#     over a bluetooth connection
    //#     There is a ROS action server providing the Bulgarian speech functionality,
    //#     which connects to this code using MQTT messages.
    //#     
    //#     #################################
    //#     
    //#     # On the ROS side:
    //#     # Set serializer/deserializer in mqtt_bridge: /home/robcoctrl/catkin_ws/src/mqtt_bridge/config/openhab_tts_stt_params.yaml
    //#     # to:   serializer: json:dumps        deserializer: json: loads
    //#
    //# ########## Publish/Subscribe to TTS (Text To Speech) Bulgarian topics ROS/MQTT communication ##########
    //# //////////////////////////////////////////////////////////////////////////////////////////////////////
    //# //
    //# // ROS topics: 
    //# //        /ttsbg_ros/tts_text           String
    //# //        /ttsbg_ros/command            String
    //# //        /ttsbg_ros/response           String
    //# // MQTT topics: 
    //# //          /ttsbg_mqtt/tts_text        String
    //# //          /ttsbg_mqtt/command         String
    //# //          /ttsbg_mqtt/response        String
    //# //
    //# // The mqtt_bridge ROS package is set to transfer the messages between ROS and the MQTT broker, between the corrresponding topics
    //# // These topics are configured in ROS mqtt_bridge package, in /home/<your user>/catkin_ws/src/mqtt_bridge/config/openhab_tts_stt_params.yaml
    //# // In the same config file, set serializer/deserializer for mqtt_bridge to:   serializer: json:dumps        deserializer: json: loads
    //# ///////////////////////////////////////////////////////////////////////////////////////////////////////
    //#     # If cloning from GitHub, You can find the required Daria Voice 
    //#     # in the SRG shared directory
    //#     #
    //#     # You can bye DARIA NUANCE VOICE from: https://harposoftware.com/en/bulgarian/179-Daria-Nuance-Voice.html
    //#     #################################



    //https://www.nuget.org/packages/M2Mqtt/
    //Install-Package M2Mqtt -Version 4.3.0


    class TTS_Bulgarian
    {
        private static SpeechSynthesizer synth = new SpeechSynthesizer();
        private static bool enableSpeaking = false;
        private static MqttClient mqttClient;

        static void Main(string[] args)
        {
            // MQTT client setup

            // Change the default MQTT broker address here
            string BrokerAddress = "192.168.1.2";

            if (args.Length == 1)
            {
                BrokerAddress = args[0];
                System.Console.WriteLine("Setting MQTT broker IP to: " + BrokerAddress);
            }
            else
            {
                System.Console.WriteLine("To change the broker IP use: TTS_Bulgarian \"MQTT_broker_IP\"");
                System.Console.WriteLine("Setting MQTT broker IP to: " + BrokerAddress);
            }

            mqttClient = new MqttClient(BrokerAddress);


            // register a callback-function called by the M2MQTT library when a message was received
            mqttClient.MqttMsgPublishReceived += client_messageReceived;
            
            var clientId = Guid.NewGuid().ToString();

            //TODO create new user pass for the MQTT broker adn replace the one below! 
            // supply - user, pass for the MQTT broker
            mqttClient.Connect(clientId, "openhabian", "robko123");


            // There is a bug when you specify more than one topic at the same time???
            //https://stackoverflow.com/questions/39917469/exception-of-type-uplibrary-networking-m2mqtt-exceptions-mqttclientexception-w
            //client.Subscribe(
            //    new string[] { "/ttsbg_mqtt/tts_text", "/ttsbg_mqtt/command" },
            //    new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

            // Subscribe to the MQTT topics with QoS 2
            mqttClient.Subscribe(new string[] { "/ttsbg_mqtt/tts_text" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            //client.Subscribe(new string[] { "/ttsbg_mqtt/command" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            synth.SetOutputToDefaultAudioDevice();
            
            // Choose the voice to speak with:
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

            string messageText = ((Encoding.UTF8.GetString(e.Message)).Replace("{\"data\": \"", "")).Replace("\"}", "");
            Console.WriteLine("Text to be spoken received => " + messageText);

            //Branch based on the incoming messages topic e.Topic.
            if (e.Topic == "/ttsbg_mqtt/tts_text")
            {
                //Cancel syntesis if currently speaking something else...
                var current = synth.GetCurrentlySpokenPrompt();
                if (current != null)
                    synth.SpeakAsyncCancel(current);
                if (enableSpeaking)
                {
                    // Speak out the string. 
                    SpeakAsync(messageText);

                    //TODO refine the command/response scheme

                    mqttClient.Publish("/ttsbg_mqtt/response", Encoding.UTF8.GetBytes("{\"data\": \"" + "speaking" + "\"}"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                }

            }
            else if (e.Topic == "/ttsbg_mqtt/command")
            {
                if (messageText == "enable")
                {
                    enableSpeaking = true;
                    mqttClient.Publish("/ttsbg_mqtt/response", Encoding.UTF8.GetBytes("{\"data\": \"" + "enabled" + "\"}"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);

                }
                else if (messageText == "disable")
                {
                    enableSpeaking = false;
                    cancelSpeaking();
                    mqttClient.Publish("/ttsbg_mqtt/response", Encoding.UTF8.GetBytes("{\"data\": \"" + "disabled" + "\"}"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);

                }
                else if (messageText == "cancel")
                {
                    // Cancel if currently speaking
                    cancelSpeaking();
                }
                //else
                //{

                //}
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

        private static void SpeakAsync(string messageText)
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
