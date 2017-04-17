﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System;

public class network_manager : MonoBehaviour
{

    static bool is_host = false;
    Canvas_Manager manager_script;
    GameObject server_lobby;
    GameObject join_lobby;
    spawner_manager spawn_man;


    //Network variables
    string server_ip;
    public bool game_ready = false;
    static byte server_players_amount = 0;

    //Server Stuff
    int server_port = 8888;
    byte server_unreliable_channel;
    byte server_reliable_channel;
    static int[] server_client_connection = new int[4];
    static int server_socket_ID;
    int max_connections = 10;


    public int server_player_control = -1;



    //Client stuff
    int client_socket_ID;
    byte client_unreliable_channel;
    byte client_reliable_channel;
    int client_connection;
    bool client_joined = false;
    public byte client_players_amount = 0;



    //public byte[] server_to_client_data_large = new byte[99];

    public bool started = false;

    public bool reliable_message = false;

    //public byte[] client_info = new byte[24];



    // Buffer Info
    // Sizes
    public int size_of_server_buffer = 295;
    public int size_of_client_buffer = 132;
    // Client Buffers
    static byte[] client_to_server_data_large = new byte[132];
    static byte[] client_reliable_buffer = new byte[132];
    static byte[] client_reliable_data_from_server = new byte[295];
    // Server Buffers
    static byte[] server_to_client_data_large = new byte[295]; // this also stores the data for the client
    static byte[] server_reliable_buffer = new byte[295];
    static byte[] server_data_from_client = new byte[132];
    static byte[] server_reliable_data_from_client = new byte[132];

    int frame = 0;

    bool no_canvas_start = false;

    bool no_canvas_client_connected = false;
    bool no_canvas_server_connected = false;



    void Start()
    {
        if (ChangeIp.ipAddress == "host")
        {
            is_host = true;
        }
        else
        {
            is_host = false;
        }

        Debug.Log(ChangeIp.ipAddress.ToString());
        Debug.Log("ip address");
        //Array.Clear(server_to_client_data, 0, 24);

        GameObject custom_network_manager = GameObject.Find("Game Manager(Clone)");
        manager_script = custom_network_manager.GetComponent<Canvas_Manager>();
        spawn_man = custom_network_manager.GetComponent<spawner_manager>();

        //is_host = manager_script.get_host_status();
        //Debug.Log(manager_script.get_host_status().ToString());
        //server_ip = manager_script.get_address();
        //server_ip = manager_script.get_inserted_ip();

        if (is_host)
        {
            server_client_connection[server_players_amount] = 0;
            server_players_amount++;
            client_players_amount = 1;

            Debug.Log("SERVER");
            server_setup();

        }

        if (!is_host )
        {
            Debug.Log("CLIENT");
            client_players_amount = 2;
            client_setup();
            connect_to_server(ChangeIp.ipAddress);
            Debug.Log(ChangeIp.ipAddress.ToString());

        }

    }


    void Update()
    {



        // Client Poll Server
        if (!no_canvas_client_connected && !is_host)
        {
            //connect_to_server(ChangeIp.ipAddress);
            client_lobby_update();
        }

        if (no_canvas_client_connected && !is_host)
        {
            if(no_canvas_start == false)
            {
                spawn_man.spawn_four_players(4);
                no_canvas_start = true;
            }
            else
             {
                if (frame == 4)
                {
                   // Debug.Log("client attempt send");
                    client_send_information(); //constant unreliable 
                    frame = 0;
                }
                else
                {
                    frame++;
                }
                //Debug.Log("client attempt recieve");

                client_recieve_data(); //only unreliable from server
            }
        }

        if (!no_canvas_server_connected && is_host)
        {
            server_game_not_ready();
        }
        if (no_canvas_server_connected && is_host)
        {
            if (no_canvas_start == false)
            {
                spawn_man.spawn_four_players(4);
                no_canvas_start = true;
            }
            else {
                if (frame == 4)
                {
                    //Debug.Log("server attempt send");

                    server_send_large_message_to_client(); //unreliable
                    frame = 0;
                }
                else
                {
                    frame++;
                }
                //Debug.Log("server attempt recieve");

                server_recieve_data(); // reliable and unreliable from clients
            }
        }

    }







    void join(string player_update)
    {
        server_lobby = GameObject.Find("Server Lobby(Clone)");
        GameObject player = server_lobby.transform.Find(player_update).gameObject;
        GameObject player_status = player.transform.Find("Player Status").gameObject;

        Text status = player_status.GetComponent<Text>();
        status.text = player_update + "\nIn Lobby";
        status.color = new Color(0, 255, 0);

        RawImage image = player.GetComponent<RawImage>();
        image.color = new Color(0, 255, 0);
    }




    // ----------------------------
    // Setup Network
    // ----------------------------

    void server_setup()
    {
        /// Global Config defines global paramters for network library.
        GlobalConfig global_configuration = new GlobalConfig();
        global_configuration.ReactorModel = ReactorModel.SelectReactor;
        global_configuration.ThreadAwakeTimeout = 10;

        /// Add a channel to send and recieve 
        /// Build channel configuration
        ConnectionConfig connection_configuration = new ConnectionConfig();
        server_reliable_channel = connection_configuration.AddChannel(QosType.Reliable);
        server_unreliable_channel = connection_configuration.AddChannel(QosType.StateUpdate);



        /// Create Network Topology for host configuration
        /// This topology defines: 
        /// (1) how many connection with default config will be supported/
        /// (2) what will be special connections (connections with config different from default).
        HostTopology host_topology = new HostTopology(connection_configuration, max_connections);

        /// Initializes the NetworkTransport. 
        /// Should be called before any other operations on the NetworkTransport are done.
        NetworkTransport.Init();

        // Open sockets for server and client
        server_socket_ID = NetworkTransport.AddHost(host_topology, server_port);
        if (server_socket_ID < 0) { Debug.Log("Server socket creation failed!"); } else { Debug.Log("Server socket creation successful!"); }

    }

    void client_setup()
    {
        /// Global Config defines global paramters for network library.
        GlobalConfig global_configuration = new GlobalConfig();
        global_configuration.ReactorModel = ReactorModel.SelectReactor;
        global_configuration.ThreadAwakeTimeout = 10;

        /// Add a channel to send and recieve 
        /// Build channel configuration
        ConnectionConfig connection_configuration = new ConnectionConfig();
        client_reliable_channel = connection_configuration.AddChannel(QosType.Reliable);
        client_unreliable_channel = connection_configuration.AddChannel(QosType.StateUpdate);


        /// Create Network Topology for host configuration
        /// This topology defines: 
        /// (1) how many connection with default config will be supported/
        /// (2) what will be special connections (connections with config different from default).
        HostTopology host_topology = new HostTopology(connection_configuration, 5);

        /// Initializes the NetworkTransport. 
        /// Should be called before any other operations on the NetworkTransport are done.
        NetworkTransport.Init();

        // Open sockets for server and client
        client_socket_ID = NetworkTransport.AddHost(host_topology);
        if (client_socket_ID < 0) { Debug.Log("Client socket creation failed!"); } else { Debug.Log("Client socket creation successful!"); }

    }

    void connect_to_server(string ip)
    {
        byte error;
        client_connection = NetworkTransport.Connect(client_socket_ID, ip, server_port, 0, out error);
        if (error != 0)
        {
            Debug.Log("I FAILED to send my request to connect to the server");
            Debug.Log(error.ToString());
        }
        else
        {
            Debug.Log("I Sent my request to connect");
        }
    }





    void server_game_not_ready()
    {
        byte error;
        //Debug.Log("Server is checking for messages...");
        int received_host_ID;
        int received_connection_ID;
        int received_channel_ID;
        int recieved_data_size;
        byte[] buffer = new byte[size_of_server_buffer];
        int data_size = size_of_server_buffer;

        NetworkEventType networkEvent = NetworkEventType.DataEvent;

        // Poll both server/client events

        networkEvent = NetworkTransport.Receive(out received_host_ID,
                                                out received_connection_ID,
                                                out received_channel_ID,
                                                buffer,
                                                data_size,
                                                out recieved_data_size,
                                                out error
                                                );

        switch (networkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Server Recieved a Connection Event");
                server_players_amount++;
                Debug.Log("Player " + server_players_amount.ToString() + " Joined");

                //join("Player " + server_players_amount.ToString());
                //Debug.Log(received_host_ID.ToString());
                //Debug.Log("ServerConnection Before: " + server_client_connection);
                server_client_connection[server_players_amount - 1] = received_connection_ID;
                //Debug.Log("ServerConnection After: " + server_client_connection);
                server_confirm_client_join(received_connection_ID, server_players_amount);
                no_canvas_server_connected = true;

                break;
        }
    }

    void server_confirm_client_join(int s_c_connection, byte players_in_game)
    {
        Debug.Log("ServerConnection After: " + server_client_connection);
        byte error;
        byte[] message = new byte[size_of_server_buffer];
        //
        //
        //
        //

        //
        //
        // cant use client_joined only known by client and this is the server code
        message[0] = 1;
        // Update client on how many people joined
        message[1] = players_in_game;
        message[2] = 0;


        NetworkTransport.Send(server_socket_ID, s_c_connection, server_unreliable_channel, message, size_of_server_buffer, out error);


        if (error != 0)
        {
            Debug.Log("Could not send");
            Debug.Log(error.ToString());
        }
        else
        {
            Debug.Log("SENT");
            ///Debug.Log("IM HERERERER 3");
        }

    }

    void client_lobby_update()
    {
        byte error;
        //Debug.Log("Server is checking for messages...");
        int received_host_ID;
        int received_connection_ID;
        int received_channel_ID;
        int recieved_data_size;
        byte[] buffer = new byte[size_of_server_buffer];
        int data_size = size_of_server_buffer;

        NetworkEventType networkEvent = NetworkEventType.DataEvent;

        // Poll both server/client events

        networkEvent = NetworkTransport.Receive(out received_host_ID,
                                                out received_connection_ID,
                                                out received_channel_ID,
                                                buffer,
                                                data_size,
                                                out recieved_data_size,
                                                out error
                                                );

        switch (networkEvent)
        {
            case NetworkEventType.Nothing:
                //Debug.Log("...");
                break;
            case NetworkEventType.ConnectEvent:
                no_canvas_client_connected = true;

                break;
            case NetworkEventType.DataEvent:
                Debug.Log("Its getting data");

                if (buffer[2] == 0)
                {
                    //client_joined = true;
                    //client_players_amount = 2;
                    //Debug.Log("Number of Players in Lobby: " + client_players_amount.ToString());

                    // Open up a joined canvas for the client
                    GameObject custom_network_manager = GameObject.Find("Game Manager(Clone)");
                    //manager_script = custom_network_manager.GetComponent<Canvas_Manager>();
                    //manager_script.waiting_in_lobby(client_players_amount);
                }

                if (buffer[2] == 1)
                {
                    Debug.Log("The Server is telling me to start the game");

                    GameObject g_manager = GameObject.Find("Game Manager(Clone)");
                    Canvas_Manager c_manager_script = g_manager.GetComponent<Canvas_Manager>();
                    c_manager_script.start_the_game();
                }

                break;
        }
    }

    void tell_clients_to_start()
    {
        byte error;
        byte[] message = new byte[size_of_server_buffer];
        //
        //
        //
        //

        //
        //
        // cant use client_joined only known by client and this is the server code
        message[0] = 1;
        // Update client on how many people joined
        message[1] = 0;
        message[2] = 1;


        NetworkTransport.Send(server_socket_ID, server_client_connection[1], server_unreliable_channel, message, size_of_server_buffer, out error);
        NetworkTransport.Send(server_socket_ID, server_client_connection[2], server_unreliable_channel, message, size_of_server_buffer, out error);
        NetworkTransport.Send(server_socket_ID, server_client_connection[3], server_unreliable_channel, message, size_of_server_buffer, out error);

        if (error != 0)
        {
            Debug.Log("Could not send");
            Debug.Log(error.ToString());
        }
        else
        {
            Debug.Log("SENT");
            ///Debug.Log("IM HERERERER 3");
        }
    }





    public bool is_the_host()
    {
        return is_host;
    }

    public byte getServerPlayersAmt()
    {
        return server_players_amount;
    }






    // ----------------------------
    // Update Buffer Functions
    // ----------------------------

    // This function updates the values of the server buffer to then be sent within the update function
    public void send_from_server(int object_case, float[] values) 
    {


        //server_to_client_data_large; // this also stores the data for the client
        //server_reliable_buffer;

        int values_amount = values.Length;

        switch (object_case) {
            case 1: // Left Hand
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 3, 28);
                break;
            case 2: // Right Hand
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 31, 28);
                break;
            case 3: // Hull Position
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 59, 12);
                break;
            case 4: // Hull Rotation
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 71, 12);
                break;
            case 5: // Turret Position
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 83, 12);
                break;
            case 6: // Turret Rotation
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 95, 12);
                break;
            case 7: // Gun Rotation
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 107, 12);
                break;
            case 8: // Left Lever
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 119, 4);
                break;
            case 9: // Right Lever
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 123, 4);
                break;
            case 10: // Vertical Crank
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 127, 4);
                break;
            case 11: // Horizontal Crank
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 131, 4);
                break;
            case 12: // AI Tank 1
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 135, 40);
                break;
            case 13: // AI Tank 2
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 175, 40);
                break;
            case 14: // AI Tank 3
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 215, 40);
                break;
            case 15: // AI Tank 4
                Buffer.BlockCopy(values, 0, server_to_client_data_large, 255, 40);
                break;
        }

        // NOW SEND
        // Send happens in the Update function for now...
        // server_send_large_message_to_client()




    }

    // This function updates the buffer that the client has of all it values to send to the server
    // AND then sends it
    public void send_from_client(int object_case, float[] values) 
        {


        //client_to_server_data_large;
        //client_reliable_buffer;

        int values_amount = values.Length;

        switch (object_case) {
            case 1: // Left Hand
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 0, 28);
                break;
            case 2: // Right Hand
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 28, 28);
                break;
            case 3: // Hull Position
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 56, 12);
                break;
            case 4: // Hull Rotation
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 68, 12);
                break;
            case 5: // Turret Position
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 80, 12);
                break;
            case 6: // Turret Rotation
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 92, 12);
                break;
            case 7: // Gun Rotation
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 104, 12);
                break;
            case 8: // Left Lever
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 116, 4);
                break;
            case 9: // Right Lever
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 120, 4);
                break;
            case 10: // Vertical Crank
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 124, 4);
                break;
            case 11: // Horizontal Crank
                Buffer.BlockCopy(values, 0, client_to_server_data_large, 128, 4);
                break;
        }

        // NOW SEND
       

    }

    // This function gets values from the SERVER buffer and returns them as floats
    public float[] client_read_server_buffer(int object_case) 
    {


        int values_amount = 0;
        float[] values_3 = new float[3];
        float[] value = new float[1];
        float[] values_7 = new float[7];
        float[] value_ai = new float[40];


        switch (object_case) {
            case 1: // Left Hand
                Buffer.BlockCopy(server_to_client_data_large, 3, values_7, 0, 28);
                values_amount = 7;
                break;
            case 2: // Right Hand
                Buffer.BlockCopy(server_to_client_data_large, 31, values_7, 0, 28);
                values_amount = 7;
                break;
            case 3: // Hull Position
                Buffer.BlockCopy(server_to_client_data_large, 59, values_3, 0, 12);
                values_amount = 3;
                break;
            case 4: // Hull Rotation
                Buffer.BlockCopy(server_to_client_data_large, 71, values_3, 0, 12);
                values_amount = 3;
                break;
            case 5: // Turret Position
                Buffer.BlockCopy(server_to_client_data_large, 83, values_3, 0, 12);
                values_amount = 3;
                break;
            case 6: // Turret Rotation
                Buffer.BlockCopy(server_to_client_data_large, 95, values_3, 0, 12);
                values_amount = 3;
                break;
            case 7: // Gun Rotation
                Buffer.BlockCopy(server_to_client_data_large, 107, values_3, 0, 12);
                values_amount = 3;
                break;
            case 8: // Left Lever
                Buffer.BlockCopy(server_to_client_data_large, 119, value, 0, 4);
                values_amount = 1;
                break;
            case 9: // Right Lever
                Buffer.BlockCopy(server_to_client_data_large, 123, value, 0, 4);
                values_amount = 1;
                break;
            case 10: // Vertical Crank
                Buffer.BlockCopy(server_to_client_data_large, 127, value, 0, 4);
                values_amount = 1;
                break;
            case 11: // Horizontal Crank
                Buffer.BlockCopy(server_to_client_data_large, 131, value, 0, 4);
                values_amount = 1;
                break;
            case 12: // AI Tank 1
                Buffer.BlockCopy(server_to_client_data_large, 135, value_ai, 0, 40);
                values_amount = 32;
                break;
            case 13: // AI Tank 2
                Buffer.BlockCopy(server_to_client_data_large, 175, value_ai, 0, 40);
                values_amount = 32;
                break;
            case 14: // AI Tank 3
                Buffer.BlockCopy(server_to_client_data_large, 215, value_ai, 0, 40);
                values_amount = 32;
                break;
            case 15: // AI Tank 4
                Buffer.BlockCopy(server_to_client_data_large, 255, value_ai, 0, 40);
                values_amount = 32;
                break;
        }
        switch(values_amount)
        {
            case 7: return values_7;
            case 1: return value;
            case 3: return values_3;
            case 32: return value_ai;
            default: return value;
        }

    }


    // This function gets values from the CLIENT buffer and returns them as floats
    public float[] server_read_client_buffer(int object_case) 
    {


        int values_amount = 0;
        float[] values_3 = new float[3];
        float[] values_7 = new float[7];
        float[] value = new float[1];
      

        switch (object_case) 
            {
                case 1: // Left Hand
                    Buffer.BlockCopy(server_data_from_client, 0, values_7, 0, 28);
                    values_amount = 7;
                    break;
                case 2: // Right Hand
                    Buffer.BlockCopy(server_data_from_client, 28, values_7, 0, 28);
                    values_amount = 7;
                    break;
                case 3: // Hull Position
                    Buffer.BlockCopy(server_data_from_client, 56, values_3, 0, 12);
                    values_amount = 3;
                    break;
                case 4: // Hull Rotation
                    Buffer.BlockCopy(server_data_from_client, 68, values_3, 0, 12);
                    values_amount = 3;
                    break;
                case 5: // Turret "Turret_Objects" Rotation Angle
                    Buffer.BlockCopy(server_data_from_client, 80, values_3, 0, 12);
                    values_amount = 3;
                    break;
                case 6: // Turret "Cannon_Base" Vertical Angle
                    Buffer.BlockCopy(server_data_from_client, 92, values_3, 0, 12);
                    values_amount = 3;
                    break;
                case 7: // Gun Rotation
                    Buffer.BlockCopy(server_data_from_client, 104, values_3, 0, 12);
                    values_amount = 3;
                    break;
                case 8: // Left Lever
                    Buffer.BlockCopy(server_data_from_client, 116, value, 0, 4);
                    values_amount = 1;
                    break;
                case 9: // Right Lever
                    Buffer.BlockCopy(server_data_from_client, 120, value, 0, 4);
                    values_amount = 1;
                    break;
                case 10: // Vertical Crank
                    Buffer.BlockCopy(server_data_from_client, 124, value, 0, 4);
                    values_amount = 1;
                    break; 
                case 11: // Horizontal Crank
                    Buffer.BlockCopy(server_data_from_client, 128, value, 0, 4);
                    values_amount = 1;
                    break;
            }

        switch (values_amount)
        {
            case 7: return values_7;
            case 1: return value;
            case 3: return values_3;
            default: return value;
        }
    }



    // ----------------------------
    // SEND FUNCTIONS
    // ----------------------------

    void server_send_large_message_to_client() {
        byte error;
        NetworkTransport.Send(server_socket_ID,
                              server_client_connection[1],
                              server_unreliable_channel,
                              server_to_client_data_large,
                              size_of_server_buffer,
                              out error);
    }

    public void client_send_information() {
        byte error;
        NetworkTransport.Send(client_socket_ID, client_connection, client_unreliable_channel, client_to_server_data_large, size_of_client_buffer, out error);

    }
    
    public void server_send_reliable() {
        byte error;
        NetworkTransport.Send(server_socket_ID, server_client_connection[1], server_reliable_channel, server_reliable_buffer, size_of_server_buffer, out error);

    }

    public void client_send_reliable() {
        byte error;
        NetworkTransport.Send(client_socket_ID, client_connection, client_reliable_channel, client_reliable_buffer, size_of_client_buffer, out error);

    }

    public void send_reliable_from_client(int object_case, float input) {
        //client_to_server_data_large;
        //client_reliable_buffer;

        float[] value = { input };
        float[] clear_buffer = { 0.0f };

        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 0, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 4, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 8, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 12, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 16, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 20, 4);

        switch (object_case) {
            case 1: // Fired
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 0, 4);
                break;
            case 2: // Tank ID 1
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 4, 4);
                break;
            case 3: // Tank ID 2
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 8, 4);
                break;
            case 4: // Tank ID 3
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 12, 4);
                break;
            case 5: // Tank ID 4
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 16, 4);
                break;
            case 6: //player destroyed
                Buffer.BlockCopy(value, 0, client_reliable_buffer, 20, 4);
                break;
        }

        client_send_reliable();
    }

    public void send_reliable_from_server(int object_case, float input)
    {
        //client_to_server_data_large;
        //client_reliable_buffer;

        float[] value = { input };
        float[] clear_buffer = { 0.0f };

        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 0, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 4, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 8, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 12, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 16, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 20, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 24, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 28, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 32, 4);
        Buffer.BlockCopy(clear_buffer, 0, client_reliable_buffer, 36, 4);

        switch (object_case)
        {
            case 1: // Fired
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 0, 4);
                break;
            case 2: // Tank ID 1
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 4, 4);
                break;
            case 3: // Tank ID 2
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 8, 4);
                break;
            case 4: // Tank ID 3
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 12, 4);
                break;
            case 5: // Tank ID 4
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 16, 4);
                break;
            case 6: //player destroyed
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 20, 4);
                break;

            // AI Tank Fire
            case 7: // AI Tank 1 Fire
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 24, 4);
                break;
            case 8: // AI Tank 2 Fire
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 28, 4);
                break;
            case 9: // AI Tank 3 Fire
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 32, 4);
                break;
            case 10: // AI Tank 4 Fire
                Buffer.BlockCopy(value, 0, server_reliable_buffer, 36, 4);
                break;

        }

        server_send_reliable();
    }




    // ----------------------------
    // RECIEVE FUNCTIONS
    // ----------------------------

    void server_recieve_data()
    {
        byte error;
        //Debug.Log("Server is checking for messages...");
        int received_host_ID;
        int received_connection_ID;
        int received_channel_ID;
        int recieved_data_size;
        byte[] buffer = new byte[size_of_client_buffer];
        int data_size = size_of_client_buffer;

        NetworkEventType networkEvent = NetworkEventType.DataEvent;

        // Poll both server/client events

        networkEvent = NetworkTransport.Receive(out received_host_ID,
                                                out received_connection_ID,
                                                out received_channel_ID,
                                                buffer,
                                                data_size,
                                                out recieved_data_size,
                                                out error
                                                );

        switch (networkEvent)
        {
            case NetworkEventType.Nothing:

                //server_player_control = server_client_connection[0];
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Server Recieved a Connection Event....here?");
                break;
            case NetworkEventType.DataEvent:
                Debug.Log("Recieved data from Player 2");
                if (received_channel_ID == server_reliable_channel)
                {
                    server_reliable_data_from_client = buffer;
                    reliable_message = true;

                }
                else
                {
                    server_data_from_client = buffer;
                    reliable_message = false;
                }

                // Thid updates the buffer and the current player

                server_player_control = 2; // Update world based on data from player 2 message

                break;
        }
    }


    void client_recieve_data() {
        byte error;
        //Debug.Log("Server is checking for messages...");
        int received_host_ID;
        int received_connection_ID;
        int received_channel_ID;
        int recieved_data_size;
        byte[] buffer = new byte[size_of_server_buffer];
        int data_size = size_of_server_buffer;

        NetworkEventType networkEvent = NetworkEventType.DataEvent;

        // Poll both server/client events

        networkEvent = NetworkTransport.Receive(out received_host_ID,
                                                out received_connection_ID,
                                                out received_channel_ID,
                                                buffer,
                                                data_size,
                                                out recieved_data_size,
                                                out error
                                                );

        switch (networkEvent) {
            case NetworkEventType.Nothing:
                //Debug.Log("No Message");
                break;
            case NetworkEventType.ConnectEvent:
                break;
            case NetworkEventType.DataEvent:
                Debug.Log("Client Recieved Data");
                if (received_channel_ID == server_reliable_channel)
                {
                    client_reliable_data_from_server = buffer;
                    reliable_message = true;
                    Debug.Log("Reliable received");

                }
                else
                {
                    server_to_client_data_large = buffer;
                    reliable_message = false;
                }
                
                break;

            case NetworkEventType.DisconnectEvent:
                Debug.Log("Client has disconnected");
                client_reconnect();
                break;
        }
    }




    public float server_read_client_reliable_buffer(int object_case)
    {


        float[] value = { 0.0f };


        switch (object_case)
        {
            case 1: // Fired?
                Buffer.BlockCopy(server_reliable_data_from_client, 0, value, 0, 4);
                break;
            case 2: // Tank ID 1
                Buffer.BlockCopy(server_reliable_data_from_client, 4, value, 0, 4);
                break;
            case 3: // Tank ID 2
                Buffer.BlockCopy(server_reliable_data_from_client, 8, value, 0, 4);
                break;
            case 4: // Tank ID 3
                Buffer.BlockCopy(server_reliable_data_from_client, 12, value, 0, 4);
                break;
            case 5: // Tank ID 4
                Buffer.BlockCopy(server_reliable_data_from_client, 16, value, 0, 4);
                break;
            case 6: // player tank destroyed
                Buffer.BlockCopy(server_reliable_data_from_client, 20, value, 0, 4);
                break;

        }

        return value[0];
    }


    public float client_read_server_reliable_buffer(int object_case)
    {


        float[] value = { 0.0f };


        switch (object_case)
        {
            case 1: // Fired?
                Buffer.BlockCopy(client_reliable_data_from_server, 0, value, 0, 4);
                break;
            case 2: // Tank ID 1
                Buffer.BlockCopy(client_reliable_data_from_server, 4, value, 0, 4);
                break;
            case 3: // Tank ID 2
                Buffer.BlockCopy(client_reliable_data_from_server, 8, value, 0, 4);
                break;
            case 4: // Tank ID 3
                Buffer.BlockCopy(client_reliable_data_from_server, 12, value, 0, 4);
                break;
            case 5: // Tank ID 4
                Buffer.BlockCopy(client_reliable_data_from_server, 16, value, 0, 4);
                break;
            case 6: // player tank destroyed
                Buffer.BlockCopy(client_reliable_data_from_server, 20, value, 0, 4);
                break;


            // AI Tank Fire
            case 7: // AI Tank 1 Fire
                Buffer.BlockCopy(client_reliable_data_from_server, 24, value, 0, 4);
                break;
            case 8: // AI Tank 2 Fire
                Buffer.BlockCopy(client_reliable_data_from_server, 28, value, 0, 4);
                break;
            case 9: // AI Tank 3 Fire
                Buffer.BlockCopy(client_reliable_data_from_server, 32, value, 0, 4);
                break;
            case 10: // AI Tank 4 Fire
                Buffer.BlockCopy(client_reliable_data_from_server, 36, value, 0, 4);
                break;





        }

        return value[0];
    }






    /// Reconnect Functions
    void client_reconnect()
    {
        byte error;
        client_connection = NetworkTransport.Connect(client_socket_ID, server_ip, server_port, 0, out error);
        if (error != 0)
        {
            Debug.Log("Client reconnect FAILED");
            Debug.Log(error.ToString());
        }
        else
        {
            Debug.Log("Client reconnect attempt");
        }
    }
























}