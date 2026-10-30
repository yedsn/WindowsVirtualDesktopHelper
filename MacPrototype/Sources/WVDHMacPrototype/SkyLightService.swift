import CoreGraphics
import Darwin
import Foundation

@_silgen_name("WVDHMoveWindow")
private func bridgedMoveWindow(_ windowID: UInt32, _ spaceID: UInt64) -> Int32

enum SkyLightError: LocalizedError {
	case unavailable(String)

	var errorDescription: String? {
		switch self {
		case .unavailable(let message):
			return message
		}
	}
}

/// Thin wrapper around the SkyLight interfaces used by mature macOS window managers.
/// This is private API and intentionally isolated from the rest of the prototype.
final class SkyLightService {
	private typealias MainConnectionFunction = @convention(c) () -> UInt32
	private typealias CopyManagedDisplaySpacesFunction = @convention(c) (UInt32) -> CFArray?
	private typealias SpaceGetTypeFunction = @convention(c) (UInt32, UInt64) -> Int32
	private typealias SetCurrentSpaceFunction = @convention(c) (UInt32, CFString, UInt64) -> Void
	private typealias CopyWindowsFunction = @convention(c) (
		UInt32,
		UInt32,
		CFArray,
		UInt32,
		UnsafeMutablePointer<UInt64>,
		UnsafeMutablePointer<UInt64>
	) -> CFArray?
	private typealias CopySpacesForWindowsFunction = @convention(c) (UInt32, Int32, CFArray) -> CFArray?
	private typealias MoveWindowsFunction = @convention(c) (UInt32, CFArray, UInt64) -> Void

	private let libraryHandle: UnsafeMutableRawPointer
	private let connection: UInt32
	private let copyManagedDisplaySpaces: CopyManagedDisplaySpacesFunction
	private let spaceGetType: SpaceGetTypeFunction
	private let setCurrentSpaceFunction: SetCurrentSpaceFunction
	private let copyWindows: CopyWindowsFunction
	private let copySpacesForWindows: CopySpacesForWindowsFunction
	private let moveWindows: MoveWindowsFunction

	init() throws {
		let path = "/System/Library/PrivateFrameworks/SkyLight.framework/SkyLight"
		guard let handle = dlopen(path, RTLD_NOW) else {
			throw SkyLightError.unavailable("无法加载 macOS SkyLight 私有框架。")
		}

		func symbol<T>(_ name: String, as type: T.Type) throws -> T {
			guard let pointer = dlsym(handle, name) else {
				throw SkyLightError.unavailable("SkyLight 接口 \(name) 不可用。")
			}
			return unsafeBitCast(pointer, to: type)
		}

		let mainConnection = try symbol("CGSMainConnectionID", as: MainConnectionFunction.self)
		copyManagedDisplaySpaces = try symbol("CGSCopyManagedDisplaySpaces", as: CopyManagedDisplaySpacesFunction.self)
		spaceGetType = try symbol("SLSSpaceGetType", as: SpaceGetTypeFunction.self)
		setCurrentSpaceFunction = try symbol("SLSManagedDisplaySetCurrentSpace", as: SetCurrentSpaceFunction.self)
		copyWindows = try symbol("SLSCopyWindowsWithOptionsAndTags", as: CopyWindowsFunction.self)
		copySpacesForWindows = try symbol("SLSCopySpacesForWindows", as: CopySpacesForWindowsFunction.self)
		moveWindows = try symbol("SLSMoveWindowsToManagedSpace", as: MoveWindowsFunction.self)
		libraryHandle = handle
		connection = mainConnection()
	}

	func spaces() -> [DesktopSpace] {
		let displays = copyManagedDisplaySpaces(connection) as? [[String: Any]] ?? []
		var result: [DesktopSpace] = []

		for (displayIndex, display) in displays.enumerated() {
			let displayUUID = stringValue(display["Display Identifier"]) ?? "display-\(displayIndex)"
			let currentSpace = display["Current Space"] as? [String: Any]
			let currentSpaceID = uint64Value(currentSpace?["id64"])
			let rawSpaces = display["Spaces"] as? [[String: Any]] ?? []
			var userSpaces: [(id: UInt64, uuid: String)] = []

			for rawSpace in rawSpaces {
				guard let id = uint64Value(rawSpace["id64"]) else { continue }
				guard spaceGetType(connection, id) == 0 else { continue }
				let uuid = stringValue(rawSpace["uuid"]) ?? "space-\(id)"
				userSpaces.append((id, uuid))
			}

			for (index, space) in userSpaces.enumerated() {
				result.append(DesktopSpace(
					id: space.id,
					uuid: space.uuid,
					displayUUID: displayUUID,
					displayIndex: displayIndex,
					index: index,
					displayCount: displays.count,
					isCurrent: space.id == currentSpaceID
				))
			}
		}

		return result
	}

	func setCurrentSpace(displayUUID: String, spaceID: UInt64) {
		setCurrentSpaceFunction(connection, displayUUID as CFString, spaceID)
	}

	func windowIDs(on spaceID: UInt64) -> [CGWindowID] {
		var setTags: UInt64 = 0
		var clearTags: UInt64 = 0
		let requestedSpaces = [NSNumber(value: spaceID)] as CFArray
		let ids = copyWindows(connection, 0, requestedSpaces, 0x7, &setTags, &clearTags) as? [NSNumber] ?? []
		return ids.map(\.uint32Value)
	}

	func spaceIDs(for windowID: CGWindowID) -> [UInt64] {
		let windows = [NSNumber(value: windowID)] as CFArray
		let ids = copySpacesForWindows(connection, 0x7, windows) as? [NSNumber] ?? []
		return ids.map(\.uint64Value)
	}

	func moveWindow(windowID: CGWindowID, to spaceID: UInt64) {
		if bridgedMoveWindow(windowID, spaceID) != 0 { return }
		let windows = [NSNumber(value: windowID)] as CFArray
		moveWindows(connection, windows, spaceID)
	}

	private func stringValue(_ value: Any?) -> String? {
		if let string = value as? String {
			return string
		}
		if let number = value as? NSNumber {
			return number.stringValue
		}
		return nil
	}

	private func uint64Value(_ value: Any?) -> UInt64? {
		if let number = value as? NSNumber {
			return number.uint64Value
		}
		if let value = value as? UInt64 {
			return value
		}
		if let value = value as? Int {
			return UInt64(max(0, value))
		}
		return nil
	}
}
