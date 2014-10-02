Feature: MarketFilters
	In order to avoid redundant updates	
	I want to be able to filter updates that haven't change a market state

Scenario: Market has 3 active selections
	Given Market with the following selections
	| Id | Name | Status | Tradable |
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	Then Market IsSuspended is false 

Scenario: Market has 3 suspended selections
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | false        |
	| 2  | Away | 1      | false        |
	| 3  | Draw | 1      | false        |	
	When Market filters are initiated
	And Market filters are applied
	Then Market IsSuspended is true 
	
Scenario: Market update was rolled back
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | false        |
	| 2  | Away | 1      | false        |
	| 3  | Draw | 1      | false        |	
	When Market filters are initiated
	And Market filters are applied
	And Update Arrives
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	And Market filters are applied
	And Rollback change
	And Update Arrives
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	And Market filters are applied
	And Commit change
	Then Market with id=TestId is not removed from snapshot
	And Market IsSuspended is true

Scenario: Market receives duplicated update after the first update was commited
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Update Arrives
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	And Market filters are applied
	Then Market with id=TestId is removed from snapshot
	

Scenario: Market initially has all selections active and later recieved an update with suspended selection
	Given Market with the following selections
	| Id | Name | Status | Tradable |
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Update Arrives 
	| Id | Name | Status | Tradable  |	
	| 2  | Away | 1      | false        |	
	And Market filters are applied
	Then Market IsSuspended is false

Scenario: Market initially has all selections active and later receives update making it all suspended
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Update Arrives 
	| Id | Name | Status | Tradable		|	
	| 1  | Home | 1      | false        |
	| 2  | Away | 1      | false        |
	| 3  | Draw | 1      | false        |	
	And Market filters are applied
	Then Market IsSuspended is true

Scenario: Market becomes partially void
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	And Update Arrives 
	| Id | Name | Status | Tradable		|	
	| 1  | Home | 3      | false        |
	| 2  | Away | 1      | true		    |
	| 3  | Draw | 3      | false        |	
	Then Market IsSuspended is false

Scenario: Market becomes partially void and is suspended
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Update Arrives 
	| Id | Name | Status | Tradable		|	
	| 1  | Home | 3      | false        |
	| 2  | Away | 1      | false	    |
	| 3  | Draw | 3      | false        |	
	And Market filters are applied
	Then Market IsSuspended is true

Scenario: Market is partially settled
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 0      | false       |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Update Arrives 
	| Id | Name | Status | Tradable | Price |
	| 1  | Home | 2      | false    |  1.0	|
	| 2  | Away | 2      | false    |   0   |
	And Market filters are applied
	And Commit change
	And Update Arrives 
	| Id | Name | Status | Tradable | Price |
	| 1  | Home | 2      | false    |    1.0|
	| 2  | Away | 2      | false    |    0  |
	| 3  | Draw | 2      | false    |    0  |
	And Market filters are applied
	And Commit change
	Then Market with id=TestId is not removed from snapshot

Scenario: Voiding markets should not be applied markets that were previously active
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 1      | true        |
	| 2  | Away | 1      | true        |
	| 3  | Draw | 1      | true        |	
	When Market filters are initiated
	And Market filters are applied
	And Fixture is over
	And Market filters are applied
	Then Market Voided=false

Scenario: Voiding markets should be applied to markets which have never been active
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Fixture is over
	And Market filters are applied
	Then Market Voided=true

Scenario: Voiding markets should be applied even when deleted market rule is applied
	Given Market with the following selections
	| Id | Name | Status | Tradable	|
	| 1  | Home | 0      | false        |
	| 2  | Away | 0      | false        |
	| 3  | Draw | 0      | false        |	
	When Market filters are initiated
	And Market filters are applied
	And Commit change
	And Fixture is over
	And Fixture has no markets
	And Market filters are applied	
	And Commit change
	Then Market Voided=true
	And Market is not duplicated

Scenario: Market rule solver must resolve correctly any conflicts
	Given a fixture with the following markets
	| Market | Name  |
	| 1      | One   |
	| 2      | Two   |
	| 3      | Three |
	| 4      | Four  |
	| 5      | Five  |
	| 6      | Six   |
	| 7      | Seven |
	| 8      | Eight |
	| 9      | Nine  |
	And A market rule with the have the following rules
	| Rule |
	| A    |
	| B    |
	| C    |
	| D    |
	And the market rules return the following intents
	| Rule | Market | Result |
	| A    | 1      | R      |
	| B    | 1      | !R     |
	| C    | 1      | E      |
	| D    | 1      | !E     |
	| A    | 2      | E      |
	| B    | 2      | !E     |
	| A    | 3      | R      |
	| B    | 3      | E      |
	| A    | 4      | !E     |
	| B    | 4      | R      |
	| A    | 5      | E      |
	| B    | 5      | E      |
	| A    | 6      | !E     |
	| B    | 6      | !R     |
	| A    | 7      | E      |
	| B    | 7      | E      |
	| C    | 7      | !E     |
	| D    | 7      | E      |
	| A    | 8      | !R     |
	| B    | 8      | E      |
	| A    | 9      | R      |
	| B    | 9      | !R     |
	When I apply the rules
	Then I must see these changes
	| Market | Name               | Exists |
	| 1      | One                | true   |
	| 2      | Two                | true   |
	| 3      | Three - E: B       | true   |
	| 4      | Four               | true   |
	| 5      | Five - E: A - E: B | true   |
	| 6      | Six                | true   |
	| 7      | Seven              | true   |
	| 8      | Eight - E: B       | true   |
	| 9      | Nine               | true   |


Scenario: Market rule solver must resolve correctly any edit conflicts
	Given a fixture with the following markets
	| Market | Name  | Selections |
	| 1      | One   | 3          |
	| 2      | Two   | 2          |
	| 3      | Three | 1          |
	| 4      | Four  | 1          |
	| 5      | Five  | 0          |
	| 6      | Six   | 0          |
	| 7      | Seven | 0          |
	And A market rule with the have the following rules
	| Rule |
	| A    |
	| B    |
	| C    |
	| D    |
	And the market rules return the following intents
	| Rule | Market | Result |
	| A    | 1      | CS     |
	| B    | 1      | AS     |
	| C    | 1      | RS     |
	| D    | 1      | CD     |
	| A    | 2      | CS     |
	| B    | 2      | RS     |
	| A    | 3      | CS     |
	| B    | 3      | CD     |
	| A    | 4      | AS     |
	| B    | 4      | RS     |
	| A    | 5      | AS     |
	| B    | 5      | CD     |
	| A    | 6      | CD     |
	| B    | 6      | RS     |
	| A    | 7      | RS     |
	| B    | 7      | RS     |
	When I apply the rules
	Then I must see these selection changes
	| Market | Name   | NumberOfSelections | Names                      |
	| 1      | OneD   | 4                  | One1A, One2A, One3A, One4B |
	| 2      | Two    | 2                  | Two1A, Two2A               |
	| 3      | ThreeB | 1                  | Three1A                    |
	| 4      | Four   | 1                  | Four1A                     |
	| 5      | FiveB  | 1                  | Five1A                     |
	| 6      | SixA   | 0                  |                            |
	| 7      | Seven  | 0                  |                            |


