using NurApiDotNet;

Boolean running = false;

NurApi? hNur = null;

NurApi.TagStorage tags = new NurApi.TagStorage();

void hNur_ConnectedEvent(object sender, NurApi.NurEventArgs e)
{
  Console.WriteLine("Connected to reader");
  hNur.TxLevel = 0;   // Set Tx power to max level
}

void hNur_DisconnectedEvent(object sender, NurApi.NurEventArgs e)
{
  hNur.Connect();
}

void hNur_InventoryStreamReady(object sender, NurApi.InventoryStreamEventArgs e)
{
  try
  {
    NurApi.TagStorage intTagStorage = hNur.GetTagStorage();
    lock (intTagStorage)
    {
      for (int i = 0; i < intTagStorage.Count; i++)
      {
        NurApi.Tag tag;
        if (tags.AddTag(intTagStorage[i], out tag))
        {
          Console.WriteLine("EPC: {0}, ANT: {1}, RSSI: {2}",
              tag.GetEpcString(), tag.antennaId, tag.rssi);
        }
        else
        {
          Console.WriteLine("EPC: {0}, ANT: {1}, RSSI: {2}",
              tag.GetEpcString(), tag.antennaId, tag.rssi);
        }
      }
      hNur.ClearTags();
    }

    if (e.data.stopped && running)
    {
      hNur.StartInventoryStream();
    }
  }
  catch (Exception ex)
  {
    Console.WriteLine("Inventory error: " + ex.Message);
  }
}

try
{
  hNur = new NurApi();

  hNur.ConnectedEvent += new EventHandler<NurApi.NurEventArgs>(hNur_ConnectedEvent);
  hNur.DisconnectedEvent += new EventHandler<NurApi.NurEventArgs>(hNur_DisconnectedEvent);

  hNur.InventoryStreamEvent += new EventHandler<NurApi.InventoryStreamEventArgs>(hNur_InventoryStreamReady);

  if (!hNur.IsConnected())
  {
    throw new Exception("Not connected to reader");
  }

  hNur.ClearTags();
  hNur.StartInventoryStream();
  running = true;
}
catch (Exception ex)
{
  running = false;
  Console.WriteLine("Could not initialize NurApi, error: " + ex.ToString());
  Environment.Exit(-1);
}