/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using CodeMonkey.Utils; // Disabled - utilities not present in project

public class Assistant : MonoBehaviour {

    [SerializeField] private Transform jokeBtn;

    private void Start() {
        // Disabled - Button_Sprite and ChatBubble utilities not present
        /*
        jokeBtn.GetComponent<Button_Sprite>().ClickFunc = () => {
            ChatBubble.Create(null, new Vector3(15.0f, -7.0f), ChatBubble.IconType.Happy, GetJoke());
        };
        */
    }

    private string GetJoke() {
        string[] messageArray = new string[] {
            "I was wondering why the ball was getting bigger, then it hit me",
            "Did you hear about the guy whose whole left side was cut off? He’s all right now",
            "I'm reading a book about anti-gravity. It's impossible to put down!",
            "Don't trust atoms. They make up everything!",
            "What did the pirate say on his 80th birthday? AYE MATEY",
            "What’s Forrest Gump’s password? 1forrest1",
            "Two guys walk into a bar, the third one ducks.",
            "How many tickles does it take to make an octopus laugh? Ten-tickles",
            "Our wedding was so beautiful, even the cake was in tiers.",
            "What do you call a dinosaur with a extensive vocabulary? A thesaurus."
        };

        return messageArray[Random.Range(0, messageArray.Length)];
    }

}
