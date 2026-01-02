namespace WebApplicationDemo.Dto
{
    public static class Examples
    {
        public static Entity.V1.Output GetEntityPayload()
        {
            return new Entity.V1.Output()
            {
                EntityId = "Identifier",
                Date = new NodaTime.LocalDate(2019, 01, 01),
                Value = 1000,
                EntityResult = EntityResult.Success2,
                EntityTest = EntityTest.Prova1
            };

        }
    }
}