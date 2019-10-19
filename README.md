# iot-edge-ic-bridge
Azure IoT Edge module which passes incoming messages to the IoT Central bridge of your choice.

## Introduction

Microsoft offers a [bridge](https://github.com/Azure/iotc-device-bridge) for third party IoT clouds to be connected with IoT Central. But this bridge is also usable by IoT Edge devices.

This module sends incoming messages to the IoT Central Bridge which is available in the Desired Properties together with a DeviceId.

*Note*: your IoT Edge device still needs its own IoT Hub for configuration and monitoring. Only telemetry is send to IoT Central.

If the message fails (timeout of Azure Function, blocked or unassociated in IoT Central) a message is made available in output 'Exception'.

More information about the IoT Central Bridge using the is available [here](https://sandervandevelde.wordpress.com/2019/10/16/bridging-gap-from-third-party-cloud-to-iot-central/).

## Docker Hub

This module is available in [Docker Hub](https://cloud.docker.com/repository/docker/svelde/iot-edge-ic-bridge).

Use it in your IoT device with tag:

```
svelde/iot-edge-ic-bridge:1.0.0-amd64
```

## Input

Messages are received on input 'input1'.

This input accepts all kinds om messages and fills it into the measurement node of the IoT Central message format:

```
{
    "device": {
        "deviceId": "[deviceId]"
    },
    "measurements": {
        "[key x]": [value x],
        "[key y]": [value y]
    }
}
```

*Note*: The name of the device is outputted in lowercase due to requirements of the IoT Central Bridge.

*Note*: IoT Central only supports one level deep key-value pair nodes within the measurements node, except for a GPS location.


## Outputs

### No regular output

This module does not send any messages to a regular route output.

### Exceptional output

In case a message can not be parsed, the IoT Central bridge rejects the message or another excpetion occurs, a message will be put on output 'Exception'.

This is the Exception format:

```
public class OutputMessage
{
  [JsonProperty(PropertyName = "status")]
  public string Status { get; set; }

  [JsonProperty(PropertyName = "result")]
  public string Result { get; set; }
}
```

## Routes

Here is an example of how to use the Exception output for routes: 

```
{
  "routes": {
    "bridgeToEcho": "FROM /messages/modules/icbridge/outputs/Exception INTO BrokeredEndpoint(\"/modules/echo/inputs/input1\")"
  }
}
```

![routeToIoTHub](/media/EdgeRouteFlow.png)

## Desired properties

The module support one DeviceId and one IoT Central Bridge Uri.

Provide these values in the desired properties:

```
"desired": {
  "deviceId": "[Device Id]",
  "uri": "[IoT Central Bridge function URI]"
}
```

## Acknowledgement

The routing image is produced with the [IoT Edge Module Flow generator](https://iotedgemoduleflow.azurewebsites.net/).

## Contribute

The code of this logic is [open source](https://github.com/sandervandevelde/iot-edge-ic-bridge) and licenced under the MIT license.

Want to contribute? Throw me a pull request....

Want to know more about me? Check out [my blog](https://blog.vandevelde-online.com).


