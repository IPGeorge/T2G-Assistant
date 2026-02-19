using System.Collections.Generic;

public class OA_ChatCompletionRequest
{
    public string model = "local-model";
    public List<OA_Message> messages;
    public float temperature = 0f;
}

public class OA_Message
{
    public string role;
    public string content;
}

// Minimal OpenAI-compatible response wrapper
public class OA_ChatCompletionResponse
{
    public List<OA_Choice> choices;
}

public class OA_Choice
{
    public OA_Message message;
}
