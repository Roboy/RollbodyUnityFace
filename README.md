## How it works


### 1. Facial Expressions

Roboy's facial expressions are animated in Unity. The corresponding repository is in https://github.com/Roboy/RoboyUnityFace  

For starting with Unity, clone the repository to your device. For seeing the actual face of Roboy in Unity, go to RoboyUnityFace/assets and open RoboyFace. All existing animations are stored in RoboyUnityFace/assets/animations. Corresponding material (added pictures to show them on the face, e.g. a moustache or sunglasses) are stored in RoboyUnityFace/assets/FaceComponents.

The connection to ROS via code (C#) is stored in RoboyUnityFace/assets/RoboyAnimator.cs

Having started RoboyFace in Unity,there is a Project window. For viewing all existing animations, first click on "face" in the Project Window. In a second step, open the "Animator" window. For viewing existing animations, press the play button on the top and trigger the animations in the "Animator" (Paramters) window. New animations can be added via the Animation window (if the animation or the Animator window is not shown, you can open it by clicking on the "Window" button at the bar on the top of Unity program). 

For all current and new animations, there is the Inspector on the right hand side, which has different functions for changing or modifying existing and new animations. 

If new animations were created, you must add transitions from the idle status to the animation status and back to the idle status. This can also be done in the Animator window. 

In addition to the old faces and expressions in Unity, the following faces & emotions were added in SS18 by animating them in Unity and can be triggered via ROS:
 - suprised Roboy
 - crying Roboy
 - irritated Roboy
 - Roboy wearing sunglasses
 - Roboy wearing spectacles
 - Roboy having a moustache
#### Emotions

Prerequesites: - Installation of Unity

To start, clone the repository https://github.com/Roboy/RoboyUnityFace.git on your computer. 

More detailed information is written in the upper section howitworks. 

In order to start the animations, it is necessary to disconnect from ROS. Otherwise, Unity will show an error message.  This can be avoided by changing the code in RoboyUnityFace/Assets/RoboyAnimator.cs . The line which must be changed looks like follows. In this line of code, the offline toggle should be set to true.

```
bool offlineToggle = true;
```

Now, all existing animations can be triggered in Unity by starting the play mode and by triggering animations in the Animator column. 

Additional faces could be added. Useful tutorials for starting with Unity can be found on YouTube and on the Unity homepage.
