# Flight Simulator Node Code for TechDays 2021 - Flight into IoT

This code allows the simulation of the ESP32 Device Telemetry, so you can test the rest of the system without needing a physical device.

## Environment Setup

- Create an IoT Hub
- Create a Device
- Copy the Primary Connection String from the Device
- Replace Placeholder from;

    `const connectionString = 'ENTER DEVICE CONNECTION STRING';`

## Launching the Application

Run the following two commands from the `01_simulation` directory after cloning the repo;

```
npm install
node flight-simulator.js
```

## Enable Routing and Tweeting

- Create a Service Bus Namespace
- Create a Queue
- Create a Service Bus Shared Access Policy with `Manage` permissions
- Add a Service Bus Queue Custom Endpoint to the IoT Hub
- Add a Telemetry Messages Route to the Custom Endpoint with the following content;

    ```JSON
    $body.altitude > 35000
    ```

- Create a Logic App
- Set the Logic App Trigger to be `Service Bus - When a Message is Received in a Queue (auto-complete)`
- Create an action for `Twitter - Post a Tweet`

## Notes: 

- Due to the Raspberry Pi Simulator project being quite old now, it doesn't support message routing.
- However, if you don't want to try routing, you can copy the contents of the `flight-simulator.js` file over the top of the Simulator Code and use that interface directly.
- Tested with Node V16.8.0