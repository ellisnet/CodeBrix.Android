Feature: Image
	An Image draws a picture inside a box, and its Stretch says what to do when the picture and
	the box are not the same shape. The picture here is two flat colours side by side, made at
	run time, so where each colour ends up on the panel says exactly what the Stretch did.

Scenario: An Image with Stretch Fill spreads the picture over the whole box
	Given the application shows a two-colour Image named "pic" 400 by 200 with its left half "Red" and its right half "Blue"
	And the Stretch of "pic" is set to "Fill"
	When the frame is captured
	Then the picture inside "pic" is 400 by 200 pixels
	And the leftmost 190 pixels of "pic" are uniformly "Red"
	And the rightmost 190 pixels of "pic" are uniformly "Blue"
	And the region of "pic" does not contain "White"

Scenario: An Image with Stretch None draws the picture at its own size
	Given the application shows a two-colour Image named "pic" 400 by 200 with its left half "Red" and its right half "Blue"
	And the Stretch of "pic" is set to "None"
	When the frame is captured
	Then the picture inside "pic" is 40 by 20 pixels
	And the leftmost 15 pixels of "pic" are uniformly "Red"
	And the rightmost 15 pixels of "pic" are uniformly "Blue"

Scenario: Uniform keeps the picture's shape where Fill squashes it
	Given the application shows a two-colour Image named "pic" 400 by 300 with its left half "Red" and its right half "Blue"
	And the Stretch of "pic" is set to "Uniform"
	When the frame is captured as "uniform"
	Then the picture inside "pic" is 400 by 200 pixels
	When the Stretch of "pic" is set to "Fill"
	And the frame is captured as "filled"
	Then the picture inside "pic" is 400 by 300 pixels
	And the leftmost 190 pixels of "pic" are uniformly "Red"
	And the rightmost 190 pixels of "pic" are uniformly "Blue"

Scenario: An Image with Stretch UniformToFill scales the picture until it covers the box
	Given the application shows a two-colour Image named "pic" 200 by 300 with its left half "Red" and its right half "Blue"
	And the Stretch of "pic" is set to "UniformToFill"
	When the frame is captured
	Then the picture inside "pic" is 200 by 300 pixels
	And the region of "pic" contains "Red"

Scenario: Changing an Image's Stretch redraws the picture
	Given the application shows a two-colour Image named "pic" 400 by 200 with its left half "Red" and its right half "Blue"
	And the Stretch of "pic" is set to "Fill"
	When the picture "pic" is captured as "filled"
	And the Stretch of "pic" is set to "None"
	And the picture "pic" is captured as "natural"
	Then the picture inside "pic" is 40 by 20 pixels
	And where the picture "pic" was in "filled" looks different in "natural"
