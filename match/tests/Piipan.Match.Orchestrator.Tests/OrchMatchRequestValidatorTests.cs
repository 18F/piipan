using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using Piipan.Match.Orchestrator;
using Xunit;

public class OrchMatchRequestValidatorTests
{
    public OrchMatchRequestValidator Validator()
    {
        return new OrchMatchRequestValidator();
    }

    [Fact]
    public void ReturnsErrorWhenDataEmpty()
    {
        // Setup
        var model = new OrchMatchRequest() {
            Data = new List<RequestPerson>()
        };
        // Act
        var result = Validator().TestValidate(model);
        // Assert
        result.ShouldHaveValidationErrorFor(result => result.Data);
    }

    [Fact]
    public void ReturnsErrorWhenDataOverMax()
    {
        // Setup
        var list = new List<RequestPerson>();
        for (int i = 0; i < 51; i++)
        {
        list.Add(new RequestPerson
        {
            First = "First",
            Middle = "Middle",
            Last = "Last",
            Dob = new DateTime(1970, 1, 1),
            Ssn = "000-00-0000"
        });
        }
        var model = new OrchMatchRequest { Data = list };
        // Act
        var result = Validator().TestValidate(model);
        // Assert
        result.ShouldHaveValidationErrorFor(result => result.Data);
    }
}

public class PersonValidatorTests
{
    public PersonValidator Validator()
    {
        return new PersonValidator();
    }

    [Fact]
    public void ReturnsErrorWhenLastNameEmpty()
    {
        var model = new RequestPerson() {
            Last = "",
            First = "Foo",
            Ssn = "123-45-6789"
        };
        var result = Validator().TestValidate(model);
        result.ShouldHaveValidationErrorFor(person => person.Last);
    }

    [Fact]
    public void ReturnsErrorWhenFirstNameEmpty()
    {
        var model = new RequestPerson()
        {
            First = "",
            Last = "Foo",
            Ssn = "123-45-6789"
        };
        var result = Validator().TestValidate(model);
        result.ShouldHaveValidationErrorFor(person => person.First);
    }

    [Fact]
    public void ReturnsErrorWhenSsnMalformed()
    {
        var model = new RequestPerson()
        {
        Last = "Foo",
        First = "Bar",
        Ssn = "baz"
        };
        var result = Validator().TestValidate(model);
        result.ShouldHaveValidationErrorFor(person => person.Ssn);
    }
}
