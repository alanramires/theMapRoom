using System.Collections.Generic;

[System.Serializable]
public class PodeDesembarcarLandingStatus
{
    public bool isValid;
    public string explanation;
}

[System.Serializable]
public class PodeDesembarcarReport
{
    public PodeDesembarcarLandingStatus localDePouso = new PodeDesembarcarLandingStatus();
    public List<PodeDesembarcarOption> locaisValidosDeDesembarque = new List<PodeDesembarcarOption>();
    public List<PodeDesembarcarInvalidOption> locaisInvalidosDeDesembarque = new List<PodeDesembarcarInvalidOption>();

    public bool canDisembark => locaisValidosDeDesembarque != null && locaisValidosDeDesembarque.Count > 0;
}
